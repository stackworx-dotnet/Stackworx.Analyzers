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

        if (UnusedMethodAnalyzerMethodMatchers.IsIgnoredCandidate(method))
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

    private static void MarkUsedSymbol(IMethodSymbol referenced, UsageState state)
    {
        // Normalize to original definition to match how candidates are stored.
        var normalized = referenced.OriginalDefinition;

        // Performance: ignore framework/library methods to keep the Used set small.
        if (UnusedMethodAnalyzerMethodMatchers.IsIgnoredUsage(normalized))
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
}
