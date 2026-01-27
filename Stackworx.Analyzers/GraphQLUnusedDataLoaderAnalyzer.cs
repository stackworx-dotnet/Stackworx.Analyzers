namespace Stackworx.Analyzers;

using System;
using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class GraphQLUnusedDataLoaderAnalyzer : DiagnosticAnalyzer
{
    public static readonly DiagnosticDescriptor UnusedDataLoaderInterfaceRule = new(
        id: "SWGQL02",
        title: "DataLoader interface appears unused",
        messageFormat: "DataLoader interface '{0}' implements GreenDonut.IDataLoader<,> but is not referenced in this compilation",
        category: "GraphQL.Usage",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description:
        "Flags interfaces extending GreenDonut.IDataLoader that appear to be unused in the current compilation.",
        customTags: [WellKnownDiagnosticTags.CompilationEnd]);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        ImmutableArray.Create(UnusedDataLoaderInterfaceRule);

    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(
            GeneratedCodeAnalysisFlags.Analyze | GeneratedCodeAnalysisFlags.ReportDiagnostics);
        context.RegisterCompilationStartAction(StartDataLoaderAnalysis);
    }

    private sealed class DataLoaderUsageState
    {
        // Candidate interfaces that implement IDataLoader<,>
        public readonly ConcurrentDictionary<INamedTypeSymbol, byte> Candidates = new(SymbolEqualityComparer.Default);

        // Interfaces referenced somewhere in the compilation (recorded independently of candidate discovery)
        public readonly ConcurrentDictionary<INamedTypeSymbol, byte> Used = new(SymbolEqualityComparer.Default);
    }

    private static void StartDataLoaderAnalysis(CompilationStartAnalysisContext context)
    {
        var iDataLoader2 = context.Compilation.GetTypeByMetadataName("GreenDonut.IDataLoader`2")
                           ?? context.Compilation.GetTypeByMetadataName("global::GreenDonut.IDataLoader`2");
        if (iDataLoader2 is null)
        {
            // GreenDonut not referenced — nothing to do.
            return;
        }

        var state = new DataLoaderUsageState();

        // Collect interface candidates that implement IDataLoader<,>
        context.RegisterSymbolAction(c => CollectDataLoaderInterfaces(c, iDataLoader2, state), SymbolKind.NamedType);

        // Track usages of those interfaces across syntax nodes.
        // IMPORTANT: syntax callbacks can run before symbol callbacks (and concurrently), so this must not depend on ordering.
        context.RegisterSyntaxNodeAction(c => TrackTypeSyntaxUsage(c, state),
            SyntaxKind.IdentifierName,
            SyntaxKind.QualifiedName);

        context.RegisterCompilationEndAction(c => ReportUnused(c, state));
    }

    private static void ReportUnused(CompilationAnalysisContext context, DataLoaderUsageState state)
    {
        foreach (var iface in state.Candidates.Keys
                     .OrderBy(s => s.Locations.FirstOrDefault()?.SourceTree?.FilePath, StringComparer.Ordinal)
                     .ThenBy(s => s.Locations.FirstOrDefault()?.SourceSpan.Start ?? 0))
        {
            if (state.Used.ContainsKey(iface))
            {
                continue;
            }

            var location = iface.Locations.FirstOrDefault();
            if (location is null)
            {
                continue;
            }

            context.ReportDiagnostic(Diagnostic.Create(
                UnusedDataLoaderInterfaceRule,
                location,
                iface.ToDisplayString()));
        }
    }

    private static void CollectDataLoaderInterfaces(
        SymbolAnalysisContext context,
        INamedTypeSymbol iDataLoader2,
        DataLoaderUsageState state)
    {
        var symbol = (INamedTypeSymbol)context.Symbol;
        if (symbol.TypeKind != TypeKind.Interface)
        {
            return;
        }

        // Only source declarations
        if (symbol.DeclaringSyntaxReferences.Length == 0)
        {
            return;
        }

        // Does it implement IDataLoader<,> ?
        var implements = symbol.AllInterfaces.Any(i =>
            SymbolEqualityComparer.Default.Equals(i.OriginalDefinition, iDataLoader2));

        if (!implements)
        {
            return;
        }

        // Normalize to original definition for stable identity.
        state.Candidates.TryAdd(symbol.OriginalDefinition, 0);
    }

    private static void TrackTypeSyntaxUsage(SyntaxNodeAnalysisContext context, DataLoaderUsageState state)
    {
        // Ignore occurrences that are part of a base-type list (e.g. 'class C : IMyDataLoader')
        // because implementing/extending an interface shouldn't be considered a "usage".
        if (context.Node.Ancestors().Any(a => a is BaseListSyntax || a is SimpleBaseTypeSyntax))
        {
            return;
        }

        // Ignore generic references like builder.AddDataLoader<,> / AddDataLoader<T1, T2>(...)
        if (context.Node.Ancestors().Any(a => a is TypeArgumentListSyntax))
        {
            return;
        }

        var symbol = context.SemanticModel.GetSymbolInfo(context.Node).Symbol;
        if (symbol is not INamedTypeSymbol namedType)
        {
            return;
        }

        // Skip if this symbol *is* the interface declaration itself
        if (namedType.Locations.Any(loc =>
                loc.SourceTree == context.Node.SyntaxTree &&
                loc.SourceSpan.Contains(context.Node.Span)))
        {
            return;
        }

        // Record usage independently of candidate discovery.
        state.Used.TryAdd(namedType.OriginalDefinition, 0);
    }
}