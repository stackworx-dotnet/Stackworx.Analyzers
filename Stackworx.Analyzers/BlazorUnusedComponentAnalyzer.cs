namespace Stackworx.Analyzers;

using System;
using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class BlazorUnusedComponentAnalyzer : DiagnosticAnalyzer
{
    public static readonly DiagnosticDescriptor UnusedComponentRule = new(
        id: "SWBLZ01",
        title: "Blazor component appears unused",
        messageFormat:
            "Blazor component '{0}' is never rendered or referenced in this compilation",
        category: "Blazor.Usage",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description:
            "Flags classes deriving from Microsoft.AspNetCore.Components.ComponentBase that are never " +
            "referenced in the current compilation. Routable components (those with an @page / [Route] " +
            "attribute) and layout components (deriving from LayoutComponentBase) are ignored because " +
            "they are reachable indirectly. Components annotated with JetBrains.Annotations.UsedImplicitly " +
            "or JetBrains.Annotations.PublicAPI are also ignored.",
        customTags: [WellKnownDiagnosticTags.CompilationEnd]);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        ImmutableArray.Create(UnusedComponentRule);

    public override void Initialize(AnalysisContext context)
    {
        // Roslyn does not guarantee ordering between analyzer callbacks and may invoke them
        // concurrently. This analyzer is implemented to be ordering-independent.
        context.EnableConcurrentExecution();

        // Razor components compile to generated C# (*.razor.g.cs), and the markup usage of one
        // component inside another (`<Foo />` => `builder.OpenComponent<Foo>(...)`) also lives in
        // generated code. We must analyze generated code to see both the candidates and their usages.
        context.ConfigureGeneratedCodeAnalysis(
            GeneratedCodeAnalysisFlags.Analyze | GeneratedCodeAnalysisFlags.ReportDiagnostics);

        context.RegisterCompilationStartAction(Start);
    }

    private sealed class ComponentUsageState
    {
        // Candidate components: classes deriving from ComponentBase that are not routable.
        public readonly ConcurrentDictionary<INamedTypeSymbol, byte> Candidates =
            new(SymbolEqualityComparer.Default);

        // Component types referenced somewhere in the compilation (recorded independently of
        // candidate discovery, since callbacks can run in any order / concurrently).
        public readonly ConcurrentDictionary<INamedTypeSymbol, byte> Used =
            new(SymbolEqualityComparer.Default);
    }

    private static void Start(CompilationStartAnalysisContext context)
    {
        var componentBase =
            context.Compilation.GetTypeByMetadataName("Microsoft.AspNetCore.Components.ComponentBase");
        if (componentBase is null)
        {
            // Blazor not referenced — nothing to do.
            return;
        }

        var routeAttribute =
            context.Compilation.GetTypeByMetadataName("Microsoft.AspNetCore.Components.RouteAttribute");

        // May be null with older / trimmed Blazor references — handled gracefully downstream.
        var layoutComponentBase =
            context.Compilation.GetTypeByMetadataName("Microsoft.AspNetCore.Components.LayoutComponentBase");

        var state = new ComponentUsageState();

        context.RegisterSymbolAction(
            c => CollectComponent(c, componentBase, layoutComponentBase, routeAttribute, state),
            SymbolKind.NamedType);

        // Track type references across the compilation. Unlike the DataLoader analyzer we DO count
        // type-argument occurrences, because `builder.OpenComponent<Foo>(...)` (the rendering signal
        // emitted by the Razor compiler) appears as a type argument.
        context.RegisterSyntaxNodeAction(
            c => TrackTypeUsage(c, state),
            SyntaxKind.IdentifierName,
            SyntaxKind.GenericName,
            SyntaxKind.QualifiedName);

        context.RegisterCompilationEndAction(c => ReportUnused(c, state));
    }

    private static void CollectComponent(
        SymbolAnalysisContext context,
        INamedTypeSymbol componentBase,
        INamedTypeSymbol? layoutComponentBase,
        INamedTypeSymbol? routeAttribute,
        ComponentUsageState state)
    {
        var symbol = (INamedTypeSymbol)context.Symbol;

        if (symbol.TypeKind != TypeKind.Class)
        {
            return;
        }

        // Abstract base components are meant to be inherited; flagging them is noisy.
        if (symbol.IsAbstract || symbol.IsStatic)
        {
            return;
        }

        // Only source declarations (this includes Razor-generated *.razor.g.cs).
        if (symbol.DeclaringSyntaxReferences.Length == 0)
        {
            return;
        }

        if (!DerivesFrom(symbol, componentBase))
        {
            return;
        }

        // Ignore layout components (deriving from LayoutComponentBase). Layouts are selected by name
        // via @layout / _Imports.razor / a router DefaultLayout — frequently from another assembly —
        // so they have no direct type reference in their own compilation, much like routable pages.
        if (layoutComponentBase is not null && DerivesFrom(symbol, layoutComponentBase))
        {
            return;
        }

        // Ignore routable components (the @page directive emits a [Route] attribute) — they are
        // reachable via the router even though no other component references them directly.
        if (IsRoutable(symbol, routeAttribute))
        {
            return;
        }

        // Skip components emitted by other source generators (StrawberryShake, NSwag, Refit, ...).
        // They are stamped with [System.CodeDom.Compiler.GeneratedCode], are not user-editable, and
        // are frequently consumed only at runtime — reporting them as unused is noise. The Razor
        // compiler does NOT put this attribute on the component classes it generates, so authored
        // .razor components remain in scope.
        if (HasGeneratedCodeAttribute(symbol))
        {
            return;
        }

        // Explicit opt-out: components reached via reflection / DI / dynamic rendering can be
        // marked with JetBrains [UsedImplicitly] / [PublicAPI] to suppress this diagnostic.
        if (HasJetBrainsPublicApiOrUsedImplicitly(symbol))
        {
            return;
        }

        state.Candidates.TryAdd(symbol.OriginalDefinition, 0);
    }

    private static bool DerivesFrom(INamedTypeSymbol symbol, INamedTypeSymbol baseType)
    {
        for (var current = symbol.BaseType; current is not null; current = current.BaseType)
        {
            if (SymbolEqualityComparer.Default.Equals(current.OriginalDefinition, baseType))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsRoutable(INamedTypeSymbol symbol, INamedTypeSymbol? routeAttribute)
    {
        if (routeAttribute is null)
        {
            return false;
        }

        return symbol.GetAttributes().Any(a =>
            a.AttributeClass is not null &&
            SymbolEqualityComparer.Default.Equals(a.AttributeClass.OriginalDefinition, routeAttribute));
    }

    private static bool HasGeneratedCodeAttribute(ISymbol symbol)
    {
        foreach (var attribute in symbol.GetAttributes())
        {
            var attrClass = attribute.AttributeClass;
            if (attrClass is null)
            {
                continue;
            }

            if (attrClass.Name is "GeneratedCodeAttribute" or "GeneratedCode")
            {
                return true;
            }

            var full = attrClass.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
            if (full.Equals("global::System.CodeDom.Compiler.GeneratedCodeAttribute", StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private static bool HasJetBrainsPublicApiOrUsedImplicitly(ISymbol symbol)
    {
        foreach (var attribute in symbol.GetAttributes())
        {
            var attrClass = attribute.AttributeClass;
            if (attrClass is null)
            {
                continue;
            }

            if (attrClass.Name is "PublicAPIAttribute" or "PublicAPI" or "UsedImplicitlyAttribute" or "UsedImplicitly")
            {
                return true;
            }

            var full = attrClass.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
            if (full.Equals("global::JetBrains.Annotations.PublicAPIAttribute", StringComparison.Ordinal)
                || full.Equals("global::JetBrains.Annotations.UsedImplicitlyAttribute", StringComparison.Ordinal)
                || full.EndsWith(".PublicAPIAttribute", StringComparison.Ordinal)
                || full.EndsWith(".UsedImplicitlyAttribute", StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private static void TrackTypeUsage(SyntaxNodeAnalysisContext context, ComponentUsageState state)
    {
        var symbol = context.SemanticModel.GetSymbolInfo(context.Node).Symbol;
        if (symbol is not INamedTypeSymbol namedType)
        {
            return;
        }

        // A type's own declaration is a ClassDeclarationSyntax (the name is a token, not an
        // IdentifierName node), so references collected here are genuine usages — including base
        // lists (inheritance is real reuse) and `OpenComponent<T>` type arguments (rendering).
        state.Used.TryAdd(namedType.OriginalDefinition, 0);
    }

    private static void ReportUnused(CompilationAnalysisContext context, ComponentUsageState state)
    {
        foreach (var component in state.Candidates.Keys
                     .OrderBy(s => s.Locations.FirstOrDefault()?.SourceTree?.FilePath, StringComparer.Ordinal)
                     .ThenBy(s => s.Locations.FirstOrDefault()?.SourceSpan.Start ?? 0))
        {
            if (state.Used.ContainsKey(component))
            {
                continue;
            }

            var location = component.Locations.FirstOrDefault();
            if (location is null)
            {
                continue;
            }

            context.ReportDiagnostic(Diagnostic.Create(
                UnusedComponentRule,
                location,
                component.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat)));
        }
    }
}
