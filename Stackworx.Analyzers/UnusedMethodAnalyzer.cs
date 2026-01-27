namespace Stackworx.Analyzers;

using System;
using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Diagnostics;
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
        // Roslyn does not guarantee ordering between analyzer callbacks (symbol/operation/etc.) and
        // may invoke them concurrently. This analyzer is implemented to be ordering-independent.
        if (!Debugger.IsAttached)
        {
            context.EnableConcurrentExecution();
        }

        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.RegisterCompilationStartAction(Start);
    }

    private sealed class UsageState
    {
        // Candidates are the methods we *might* report.
        public readonly ConcurrentDictionary<IMethodSymbol, byte> Candidates = new(SymbolEqualityComparer.Default);

        // Used contains *method symbols seen in the compilation* (recorded independently of candidate discovery).
        public readonly ConcurrentDictionary<IMethodSymbol, byte> Used = new(SymbolEqualityComparer.Default);
    }

    private static void Start(CompilationStartAnalysisContext context)
    {
        var state = new UsageState();

        context.RegisterSymbolAction(c => CollectCandidate((IMethodSymbol)c.Symbol, state), SymbolKind.Method);

        context.RegisterOperationAction(c => MarkUsed(c, state),
            OperationKind.Invocation,
            OperationKind.MethodReference,
            OperationKind.DelegateCreation);

        context.RegisterCompilationEndAction(c => ReportUnused(c, state));
    }

    private static bool ShouldIgnoreCandidate(IMethodSymbol method)
    {
        var ns = method.ContainingNamespace?.ToDisplayString() ?? string.Empty;
        if (string.IsNullOrEmpty(ns))
        {
            return false;
        }

        // Same filtering concept as ShouldIgnoreUsage, but for candidates.
        // In practice candidates are usually source-only anyway, but this also guards against
        // edge cases (e.g., linked files / unusual compilations).

        // Framework/BCL
        if (ns.Equals("System", StringComparison.Ordinal)
            || ns.StartsWith("System.", StringComparison.Ordinal))
        {
            return true;
        }

        // HotChocolate + friends
        if (ns.Equals("HotChocolate", StringComparison.Ordinal)
            || ns.StartsWith("HotChocolate.", StringComparison.Ordinal)
            || ns.Equals("GreenDonut", StringComparison.Ordinal)
            || ns.StartsWith("GreenDonut.", StringComparison.Ordinal))
        {
            return true;
        }

        // EF Core
        if (ns.Equals("Microsoft.EntityFrameworkCore", StringComparison.Ordinal)
            || ns.StartsWith("Microsoft.EntityFrameworkCore.", StringComparison.Ordinal))
        {
            return true;
        }

        return false;
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
        if (method is {
                Name: WellKnownMemberNames.TopLevelStatementsEntryPointMethodName,
                ContainingType.Name: WellKnownMemberNames.TopLevelStatementsEntryPointTypeName })
        {
            return;
        }

        // Only ordinary methods (exclude ctors, accessors, operators, local functions, etc.).
        if (method.MethodKind != MethodKind.Ordinary)
        {
            return;
        }

        // Ignore Dispose pattern: Dispose / DisposeAsync are commonly called indirectly (using/await using/DI/framework/containers).
        if (IsDisposeMethod(method) || IsDisposeAsyncMethod(method) || IsHostedServiceLifecycleMethod(method))
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

        // Also ignore implicit interface implementations (e.g., 'void IFoo.M()' implemented as 'public void M()').
        // Such methods can be invoked through an interface reference, so reporting them as unused is noisy.
        if (IsImplicitInterfaceImplementation(method))
        {
            return;
        }

        // Ignore Azure Functions entry points.
        if (HasAzureFunctionsWorkerFunctionAttribute(method))
        {
            return;
        }

        // Ignore HotChocolate DataLoader methods (used by source generators/runtime).
        if (HasHotChocolateDataLoaderAttribute(method))
        {
            return;
        }

        // Ignore EF Core IEntityTypeConfiguration<T>.Configure implementations.
        if (IsEfCoreEntityTypeConfigurationConfigureMethod(method))
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

        // Ignore extension methods: their call-sites may exist in other projects/compilations and
        // usage can be hard to infer reliably. Avoid noisy false positives.
        if (method.IsExtensionMethod)
        {
            return;
        }

        // Extra perf/noise filter: don't even track candidates in common framework/library namespaces.
        if (ShouldIgnoreCandidate(method))
        {
            return;
        }

        // Normalize to original definition to reduce identity mismatches.
        var key = method.OriginalDefinition;
        state.Candidates.TryAdd(key, 0);

        // If it was already observed as used (operation actions can run first), keep it used.
        // We don't need to do anything else here because Used is a set.
    }

    private static void MarkUsed(OperationAnalysisContext context, UsageState state)
    {
        IMethodSymbol? referenced = context.Operation switch
        {
            IInvocationOperation invocation => invocation.TargetMethod,
            IMethodReferenceOperation methodRef => methodRef.Method,
            IDelegateCreationOperation del => del.Target switch
            {
                IMethodReferenceOperation mr => mr.Method,
                _ => null
            },
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

    private static bool ShouldIgnoreUsage(IMethodSymbol method)
    {
        var ns = method.ContainingNamespace?.ToDisplayString() ?? string.Empty;
        if (string.IsNullOrEmpty(ns))
        {
            return false;
        }

        // Framework/BCL
        if (ns.Equals("System", StringComparison.Ordinal)
            || ns.StartsWith("System.", StringComparison.Ordinal))
        {
            return true;
        }

        // HotChocolate + friends
        if (ns.Equals("HotChocolate", StringComparison.Ordinal)
            || ns.StartsWith("HotChocolate.", StringComparison.Ordinal)
            || ns.Equals("GreenDonut", StringComparison.Ordinal)
            || ns.StartsWith("GreenDonut.", StringComparison.Ordinal))
        {
            return true;
        }

        // EF Core
        if (ns.Equals("Microsoft.EntityFrameworkCore", StringComparison.Ordinal)
            || ns.StartsWith("Microsoft.EntityFrameworkCore.", StringComparison.Ordinal))
        {
            return true;
        }

        return false;
    }

    private static void MarkUsedSymbol(IMethodSymbol referenced, UsageState state)
    {
        // Normalize to original definition to match how candidates are stored.
        var normalized = referenced.OriginalDefinition;

        // Performance: ignore framework/library methods to keep the Used set small.
        if (ShouldIgnoreUsage(normalized))
        {
            return;
        }

        state.Used.TryAdd(normalized, 0);
    }

    private static void ReportUnused(CompilationAnalysisContext context, UsageState state)
    {
        foreach (var method in state.Candidates.Keys
                     .OrderBy(m => m.Locations.FirstOrDefault()?.SourceTree?.FilePath, StringComparer.Ordinal)
                     .ThenBy(m => m.Locations.FirstOrDefault()?.SourceSpan.Start ?? 0))
        {
            if (state.Used.ContainsKey(method))
            {
                continue;
            }

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

    private static bool IsHostedServiceLifecycleMethod(IMethodSymbol method)
    {
        if (method.Parameters.Length != 1)
        {
            return false;
        }

        if (method.Name is not ("StartAsync" or "StopAsync"))
        {
            return false;
        }

        // StartAsync/StopAsync return Task.
        if (method.ReturnType is not INamedTypeSymbol returnType
            || returnType.ContainingNamespace.ToDisplayString() != "System.Threading.Tasks"
            || returnType.Name != "Task")
        {
            return false;
        }

        // Parameter is CancellationToken.
        var p0 = method.Parameters[0].Type;
        if (p0.ContainingNamespace.ToDisplayString() != "System.Threading"
            || p0.Name != "CancellationToken")
        {
            return false;
        }

        var containing = method.ContainingType;
        if (containing is null)
        {
            return false;
        }

        // Compare by metadata name to avoid needing the hosting ref in the analyzer project.
        return containing.AllInterfaces.Any(i => i.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat) == "global::Microsoft.Extensions.Hosting.IHostedService");
    }

    private static bool HasAzureFunctionsWorkerFunctionAttribute(ISymbol symbol)
    {
        foreach (var attribute in symbol.GetAttributes())
        {
            var attrClass = attribute.AttributeClass;
            if (attrClass is null)
            {
                continue;
            }

            // Attribute can be referenced as [Function] or [FunctionAttribute]
            if (attrClass.Name is "FunctionAttribute" or "Function")
            {
                var full = attrClass.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
                if (full.Equals("global::Microsoft.Azure.Functions.Worker.FunctionAttribute", StringComparison.Ordinal)
                    || full.EndsWith(".Microsoft.Azure.Functions.Worker.FunctionAttribute", StringComparison.Ordinal)
                    || full.EndsWith(".Functions.Worker.FunctionAttribute", StringComparison.Ordinal))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static bool HasHotChocolateDataLoaderAttribute(ISymbol symbol)
    {
        foreach (var attribute in symbol.GetAttributes())
        {
            var attrClass = attribute.AttributeClass;
            if (attrClass is null)
            {
                continue;
            }

            if (attrClass.Name is not ("DataLoaderAttribute" or "DataLoader"))
            {
                continue;
            }

            var full = attrClass.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
            if (full.Equals("global::HotChocolate.DataLoaderAttribute", StringComparison.Ordinal)
                || full.EndsWith(".HotChocolate.DataLoaderAttribute", StringComparison.Ordinal)
                || full.EndsWith(".DataLoaderAttribute", StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsEfCoreEntityTypeConfigurationConfigureMethod(IMethodSymbol method)
    {
        if (method.Name != "Configure" || method.Parameters.Length != 1)
        {
            return false;
        }

        // Must be an implementation of Microsoft.EntityFrameworkCore.IEntityTypeConfiguration<T>.Configure
        // so we avoid ignoring random Configure(...) methods.
        var p0 = method.Parameters[0].Type;
        if (p0 is not INamedTypeSymbol namedParam)
        {
            return false;
        }

        // Expect EntityTypeBuilder<TEntity>
        if (namedParam.Name != "EntityTypeBuilder" || namedParam.TypeArguments.Length != 1)
        {
            return false;
        }

        // Confirm containing namespace is Microsoft.EntityFrameworkCore.Metadata.Builders
        if (namedParam.ContainingNamespace.ToDisplayString() != "Microsoft.EntityFrameworkCore.Metadata.Builders")
        {
            return false;
        }

        var containing = method.ContainingType;
        if (containing is null)
        {
            return false;
        }

        // No SpecialType for IEntityTypeConfiguration<T>, so compare by metadata name.
        return containing.AllInterfaces.Any(i =>
            i.IsGenericType
            && i.Name == "IEntityTypeConfiguration"
            && i.TypeArguments.Length == 1
            && i.ContainingNamespace.ToDisplayString() == "Microsoft.EntityFrameworkCore"
            && i.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)
                .StartsWith("global::Microsoft.EntityFrameworkCore.IEntityTypeConfiguration<", StringComparison.Ordinal));
    }

    private static bool IsImplicitInterfaceImplementation(IMethodSymbol method)
    {
        var containing = method.ContainingType;
        if (containing is null)
        {
            return false;
        }

        // Ignore common non-user-facing methods already filtered by MethodKind above.
        if (method.MethodKind != MethodKind.Ordinary)
        {
            return false;
        }

        // Check whether any implemented interface member maps to this concrete method.
        // Using OriginalDefinition keeps generic interface methods consistent.
        foreach (var iface in containing.AllInterfaces)
        {
            foreach (var member in iface.GetMembers())
            {
                if (member is not IMethodSymbol interfaceMethod)
                {
                    continue;
                }

                // Skip explicit implementations (handled elsewhere).
                if (interfaceMethod.MethodKind != MethodKind.Ordinary)
                {
                    continue;
                }

                var impl = containing.FindImplementationForInterfaceMember(interfaceMethod);
                if (impl is IMethodSymbol implMethod
                    && SymbolEqualityComparer.Default.Equals(implMethod.OriginalDefinition, method.OriginalDefinition))
                {
                    return true;
                }
            }
        }

        return false;
    }
}
