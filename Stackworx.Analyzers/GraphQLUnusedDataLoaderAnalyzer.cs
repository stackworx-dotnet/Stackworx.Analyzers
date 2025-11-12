namespace Stackworx.Analyzers;

using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.CSharp.Syntax;

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

        // Ignore occurrences that are part of a base-type list (e.g. 'class C : IMyDataLoader')
        // because class/interface declarations implementing/extending the interface shouldn't
        // be considered a "usage" for the purpose of detecting unused IDataLoader interfaces.
        if (context.Node.Ancestors().Any(a => a is BaseListSyntax || a is SimpleBaseTypeSyntax))
            return;

        var model = context.SemanticModel;
        var symbol = model.GetSymbolInfo(context.Node, context.CancellationToken).Symbol as INamedTypeSymbol;
        if (symbol is null)
            return;

        // Exclude usages that occur as type-arguments to IRequestExecutorBuilder.AddDataLoader<TInterface, TImpl>()
        // i.e. builder.AddDataLoader<IMyDataLoader, MyDataLoader>();
        // In that scenario the interface type argument should not be considered a usage for our purposes.
        var node = context.Node;
        if (node.Parent is TypeArgumentListSyntax typeArgList &&
            typeArgList.Parent is GenericNameSyntax genericName &&
            genericName.Parent is ExpressionSyntax expr)
        {
            // Walk up to InvocationExpression if present
            if (expr.Parent is InvocationExpressionSyntax invocation)
            {
                // Determine the method name and receiver type
                var invokedSymbol = model.GetSymbolInfo(invocation.Expression, context.CancellationToken).Symbol;
                if (invokedSymbol is IMethodSymbol methodSym)
                {
                    if (methodSym.Name == "AddDataLoader")
                    {
                        // Check receiver implements IRequestExecutorBuilder or the method's containing type name
                        var receiver = methodSym.ReceiverType ?? methodSym.ContainingType;
                        if (receiver != null && receiver.Name == "IRequestExecutorBuilder")
                        {
                            // don't count this as a usage
                            return;
                        }
                    }
                }
            }
        }

        // If this symbol is (or reduces to) one of our candidate interfaces, mark it as used.
        // Only consider direct references to interface symbols or references that resolve to an
        // interface type. Don't count class declarations that implement the interface as usages
        // (e.g. 'class MyDataLoader : IMyDataLoader') because we care about references to the
        // interface type, not implementations.
        foreach (var candidate in state.Candidates.Keys)
        {
            // Direct match to the interface symbol
            if (SymbolEqualityComparer.Default.Equals(symbol, candidate) ||
                SymbolEqualityComparer.Default.Equals(symbol.OriginalDefinition, candidate))
            {
                if (symbol.TypeKind == TypeKind.Interface)
                {
                    state.Candidates[candidate] = true;
                    return;
                }
            }

            // If the symbol is an interface or a constructed generic of an interface,
            // check its AllInterfaces chain for the candidate.
            if (symbol.TypeKind == TypeKind.Interface || symbol.TypeKind == TypeKind.TypeParameter)
            {
                if (symbol.AllInterfaces.Any(i => SymbolEqualityComparer.Default.Equals(i, candidate)))
                {
                    state.Candidates[candidate] = true;
                    return;
                }
            }
        }
    }
}