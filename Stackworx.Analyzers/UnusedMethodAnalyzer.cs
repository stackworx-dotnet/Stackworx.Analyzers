namespace Stackworx.Analyzers;

using System;
using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class UnusedMethodAnalyzer : DiagnosticAnalyzer
{
    public static readonly DiagnosticDescriptor UnusedMethodRule = new(
        id: "SW002",
        title: "Method appears unused",
        messageFormat: "Method '{0}' is never referenced in this compilation",
        category: "Usage",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: false,
        description:
            "Flags methods that have no call sites in the current compilation. Methods annotated with JetBrains.Annotations.PublicAPI or JetBrains.Annotations.UsedImplicitly (or whose containing type is annotated) are ignored.",
        customTags: [WellKnownDiagnosticTags.CompilationEnd]);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        ImmutableArray.Create(UnusedMethodRule);

    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.RegisterCompilationStartAction(Start);
    }

    private sealed class UsageState
    {
        public readonly ConcurrentDictionary<IMethodSymbol, bool> Candidates = new(SymbolEqualityComparer.Default);
    }

    private static void Start(CompilationStartAnalysisContext context)
    {
        var state = new UsageState();

        // Collect method candidates.
        context.RegisterSymbolAction(c => CollectCandidate((IMethodSymbol)c.Symbol, state), SymbolKind.Method);

        // Mark candidates as used based on method references in the compilation.
        context.RegisterOperationAction(c => MarkUsed(c, state), OperationKind.Invocation, OperationKind.MethodReference);

        context.RegisterCompilationEndAction(c => ReportUnused(c, state));
    }

    private static void CollectCandidate(IMethodSymbol method, UsageState state)
    {
        if (method.IsImplicitlyDeclared)
        {
            return;
        }

        // Only source declarations.
        if (method.DeclaringSyntaxReferences.Length == 0)
        {
            return;
        }

        // Top-level statements generate a synthetic 'Program' type.
        // We ignore methods in that type to avoid noisy diagnostics for minimal apps.
        if (method.ContainingType is { Name: "Program", IsImplicitlyDeclared: true })
        {
            return;
        }

        // Only ordinary methods (exclude ctors, accessors, operators, local functions, etc.).
        if (method.MethodKind != MethodKind.Ordinary)
        {
            return;
        }

        // Ignore Dispose pattern: Dispose / DisposeAsync are commonly called indirectly (using/await using/DI/framework/containers).
        if (IsDisposeMethod(method) || IsDisposeAsyncMethod(method))
        {
            return;
        }

        // Ignore overrides: they might be invoked polymorphically.
        if (method.IsOverride)
        {
            return;
        }

        // Ignore interface implementations: may be called through interface.
        if (method.ExplicitInterfaceImplementations.Length > 0)
        {
            return;
        }

        // Respect JetBrains annotations.
        if (HasJetBrainsPublicApiOrUsedImplicitly(method) || HasJetBrainsPublicApiOrUsedImplicitly(method.ContainingType))
        {
            return;
        }

        // Ignore methods inside implicitly used containing types (walk containing types for nested types).
        for (var type = method.ContainingType?.ContainingType; type is not null; type = type.ContainingType)
        {
            if (HasJetBrainsPublicApiOrUsedImplicitly(type))
            {
                return;
            }
        }

        state.Candidates.TryAdd(method, false);
    }

    private static void MarkUsed(OperationAnalysisContext context, UsageState state)
    {
        if (state.Candidates.IsEmpty)
        {
            return;
        }

        IMethodSymbol? referenced = context.Operation switch
        {
            IInvocationOperation invocation => invocation.TargetMethod,
            IMethodReferenceOperation methodRef => methodRef.Method,
            _ => null
        };

        if (referenced is null)
        {
            return;
        }

        MarkUsedSymbol(referenced, state);

        // Also consider the original definition (generic methods).
        if (!SymbolEqualityComparer.Default.Equals(referenced, referenced.OriginalDefinition))
        {
            MarkUsedSymbol(referenced.OriginalDefinition, state);
        }

        // Reduced extension method call-sites reference a reduced method.
        if (referenced.ReducedFrom is not null)
        {
            MarkUsedSymbol(referenced.ReducedFrom, state);
            if (!SymbolEqualityComparer.Default.Equals(referenced.ReducedFrom, referenced.ReducedFrom.OriginalDefinition))
            {
                MarkUsedSymbol(referenced.ReducedFrom.OriginalDefinition, state);
            }
        }

        // Mark overrides/base relationships as used too, to reduce false positives.
        if (referenced.OverriddenMethod is not null)
        {
            MarkUsedSymbol(referenced.OverriddenMethod, state);
        }

        foreach (var impl in referenced.ExplicitInterfaceImplementations)
        {
            MarkUsedSymbol(impl, state);
        }
    }

    private static void MarkUsedSymbol(IMethodSymbol referenced, UsageState state)
    {
        if (state.Candidates.ContainsKey(referenced))
        {
            state.Candidates[referenced] = true;
            return;
        }

        // Handle comparing by OriginalDefinition for stored candidates.
        foreach (var candidate in state.Candidates.Keys)
        {
            if (SymbolEqualityComparer.Default.Equals(candidate, referenced))
            {
                state.Candidates[candidate] = true;
                return;
            }

            if (SymbolEqualityComparer.Default.Equals(candidate.OriginalDefinition, referenced.OriginalDefinition))
            {
                state.Candidates[candidate] = true;
                return;
            }
        }
    }

    private static void ReportUnused(CompilationAnalysisContext context, UsageState state)
    {
        foreach (var kvp in state.Candidates.OrderBy(k => k.Key.Locations.FirstOrDefault()?.SourceTree?.FilePath, StringComparer.Ordinal)
                     .ThenBy(k => k.Key.Locations.FirstOrDefault()?.SourceSpan.Start ?? 0))
        {
            if (kvp.Value)
            {
                continue;
            }

            var method = kvp.Key;
            var location = method.Locations.FirstOrDefault();
            if (location is null)
            {
                continue;
            }

            context.ReportDiagnostic(
                Diagnostic.Create(
                    UnusedMethodRule,
                    location,
                    method.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat)));
        }
    }

    private static bool HasJetBrainsPublicApiOrUsedImplicitly(ISymbol? symbol)
    {
        if (symbol is null)
        {
            return false;
        }

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

    private static bool IsDisposeMethod(IMethodSymbol method)
    {
        if (method.Name != "Dispose" || method.Parameters.Length != 0)
        {
            return false;
        }

        // Treat explicit interface implementation and normal implementation the same.
        var containing = method.ContainingType;
        if (containing is null)
        {
            return false;
        }

        foreach (var iface in containing.AllInterfaces)
        {
            if (iface.SpecialType == SpecialType.System_IDisposable)
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsDisposeAsyncMethod(IMethodSymbol method)
    {
        if (method.Name != "DisposeAsync" || method.Parameters.Length != 0)
        {
            return false;
        }

        // IAsyncDisposable.DisposeAsync returns ValueTask.
        if (method.ReturnType is not INamedTypeSymbol named
            || named.ContainingNamespace.ToDisplayString() != "System.Threading.Tasks"
            || named.Name != "ValueTask")
        {
            return false;
        }

        var containing = method.ContainingType;
        if (containing is null)
        {
            return false;
        }

        // No SpecialType for IAsyncDisposable, so compare by metadata name.
        return containing.AllInterfaces.Any(i => i.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat) == "global::System.IAsyncDisposable");
    }
}
