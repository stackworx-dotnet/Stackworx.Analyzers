namespace Stackworx.Analyzers;

using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
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
        public readonly ConcurrentDictionary<INamedTypeSymbol, bool> Candidates = new(SymbolEqualityComparer.Default);
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

        // Track usages of those interfaces across syntax nodes
        context.RegisterSyntaxNodeAction(c => TrackTypeSyntaxUsage(c, state),
            SyntaxKind.IdentifierName,
            SyntaxKind.QualifiedName,
            SyntaxKind.GenericName);

        // At compilation end, report any interfaces that were never referenced
        context.RegisterCompilationEndAction(c =>
        {
            foreach (var kvp in state.Candidates)
            {
                var iface = kvp.Key;
                var used = kvp.Value;
                if (!used)
                {
                    var location = iface.Locations.FirstOrDefault();
                    if (location != null)
                    {
                        c.ReportDiagnostic(Diagnostic.Create(UnusedDataLoaderInterfaceRule, location,
                            iface.ToDisplayString()));
                    }
                }
            }
        });
    }

    private static void CollectDataLoaderInterfaces(SymbolAnalysisContext context, INamedTypeSymbol iDataLoader2,
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
        // Either directly or via inheritance chain.
        var implements = symbol.AllInterfaces.Any(i =>
            SymbolEqualityComparer.Default.Equals(i.OriginalDefinition, iDataLoader2));

        if (implements)
        {
            state.Candidates.TryAdd(symbol, false);
        }
    }

    private static void TrackTypeSyntaxUsage(SyntaxNodeAnalysisContext context, DataLoaderUsageState state)
    {
        if (state.Candidates.IsEmpty)
            return;

        var model = context.SemanticModel;
        var symbol = model.GetSymbolInfo(context.Node, context.CancellationToken).Symbol as INamedTypeSymbol;
        if (symbol is null)
            return;

        // If this symbol is (or reduces to) one of our candidate interfaces, mark it as used
        foreach (var candidate in state.Candidates.Keys)
        {
            if (SymbolEqualityComparer.Default.Equals(symbol, candidate) ||
                SymbolEqualityComparer.Default.Equals(symbol.OriginalDefinition, candidate))
            {
                state.Candidates[candidate] = true;
                return;
            }

            // Also consider constructed generic instantiations or aliasing
            if (symbol.AllInterfaces.Any(i => SymbolEqualityComparer.Default.Equals(i, candidate)))
            {
                state.Candidates[candidate] = true;
                return;
            }
        }
    }
}