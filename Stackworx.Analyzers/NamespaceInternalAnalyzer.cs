namespace Stackworx.Analyzers;

using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class NamespaceInternalAnalyzer : DiagnosticAnalyzer
{
    public const string Id_Reference = "SW101";
    public const string Id_Using = "SW102";

    private static readonly LocalizableString Title1 =
        "Forbidden reference to feature-internal namespace";
    private static readonly LocalizableString Message1 =
        "‘{0}’ is internal to feature ‘{1}’ and may only be used within ‘{2}’";
    private static readonly LocalizableString Desc1 =
        "Code outside a feature cannot import or reference that feature’s Internal namespace.";

    private static readonly LocalizableString Title2 =
        "Forbidden using to feature-internal namespace";
    private static readonly LocalizableString Message2 =
        "Using directive targets internal namespace ‘{0}’; only code under ‘{1}’ may import it";
    private static readonly LocalizableString Desc2 =
        "Using directives that import a feature’s Internal namespace are restricted to that feature.";

    private static readonly DiagnosticDescriptor RuleReference = new(
        Id_Reference, Title1, Message1, "Architecture", DiagnosticSeverity.Warning, isEnabledByDefault: true, description: Desc1);

    private static readonly DiagnosticDescriptor RuleUsing = new(
        Id_Using, Title2, Message2, "Architecture", DiagnosticSeverity.Warning, isEnabledByDefault: true, description: Desc2);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        ImmutableArray.Create(RuleReference, RuleUsing);

    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

        // Disable for now until duplicate errors can be removed
        /*
        context.RegisterCompilationStartAction(compilationStart =>
        {
            var violations = new ConcurrentBag<Violation>();
            
            // Analyze symbol/name references
            compilationStart.RegisterSyntaxNodeAction(ctx =>
                {
                    var config = AnalyzerConfig.Read(ctx); // per-syntax-tree (.editorconfig) read
                    if (config.Roots.IsDefaultOrEmpty || config.IsTestProject)
                    {
                        return;
                    }

                    if (AnalyzeNameLike(ctx, config, out var violation))
                    {
                        violations.Add(violation);
                    }
                },
                SyntaxKind.IdentifierName,
                SyntaxKind.QualifiedName,
                SyntaxKind.GenericName,
                SyntaxKind.AliasQualifiedName,
                // optional but recommended to catch inheritance & constraints too:
                SyntaxKind.SimpleBaseType,
                SyntaxKind.TypeConstraint);

            compilationStart.RegisterCompilationEndAction(context =>
            {
                // TODO: de-duplicate violations
                var count = violations.Count;
                //Console.WriteLine(violations.Count.ToString());
                // TODO
                // Outside feature: forbidden
                // context.ReportDiagnostic(Diagnostic.Create(
                //     RuleReference, node.GetLocation(),
                //     targetNs, feature, internalRoot));
            });
        });
        */
      
        context.RegisterCompilationStartAction(compilationStart =>
        {
            // Analyze using directives
            compilationStart.RegisterSyntaxNodeAction(ctx =>
                {
                    var config = AnalyzerConfig.Read(ctx); // per-syntax-tree (.editorconfig) read
                    if (config.Roots.IsDefaultOrEmpty || config.IsTestProject)
                    {
                        return;
                    }

                    AnalyzeUsing(ctx, config);
                },
                SyntaxKind.UsingDirective);
        });
    }

    private static void AnalyzeUsing(SyntaxNodeAnalysisContext context, AnalyzerConfig config)
    {
        var usingDirective = (UsingDirectiveSyntax)context.Node;
        var nsText = usingDirective.Name?.ToString();

        // TODO: when can this be null?
        if (nsText is not null)
        {
            if (TryMatchInternal(nsText, config.Roots, out var root, out var feature, out var internalRoot))
            {
                var accessorNs = GetAccessorNamespace(context);
                if (!IsWithinFeature(accessorNs, root, feature))
                {
                    context.ReportDiagnostic(Diagnostic.Create(
                        RuleUsing, usingDirective.Name?.GetLocation(),
                        nsText, feature, internalRoot));
                }
            }
        }
    }

    private static bool AnalyzeNameLike(
        SyntaxNodeAnalysisContext context,
        AnalyzerConfig config,
        [NotNullWhen(true)]
        out Violation? violation)
    {
        var node = (ExpressionSyntax)context.Node;
        var model = context.SemanticModel;

        // Symbol for name expression (type, member, etc.)
        var symbol = model.GetSymbolInfo(node).Symbol
                     ?? model.GetTypeInfo(node).Type as ISymbol
                     ?? model.GetDeclaredSymbol(node);
        if (symbol is null)
        {
            violation = null;
            return false;
        }

        // Get target namespace for the symbol (for members, climb to containing type)
        var targetNs = (symbol as INamespaceSymbol)?.ToDisplayString()
                       ?? symbol.ContainingNamespace?.ToDisplayString();

        if (string.IsNullOrEmpty(targetNs))
        {
            violation = null;
            return false;
        }

        if (!TryMatchInternal(targetNs!, config.Roots, out var root, out var feature, out var internalRoot))
        {
            violation = null;
            return false;
        }

        var accessorNs = GetAccessorNamespace(context);
        if (IsWithinFeature(accessorNs, root, feature))
        {
            violation = null;
            return false;
        }

        violation = new Violation
        {
            Node = node,
            TargetNs = targetNs,
            Feature = feature,
            InternalRoot = internalRoot,
        };

        return true;
        // Outside feature: forbidden
        // context.ReportDiagnostic(Diagnostic.Create(
        //     RuleReference, node.GetLocation(),
        //     targetNs, feature, internalRoot));
    }

    /// <summary>
    /// Given a fully-qualified namespace string (e.g., "Contoso.Features.Feature1.Internal.Sub"),
    /// detect if it is within a feature "Internal" tree for any configured root.
    /// </summary>
    private static bool TryMatchInternal(string ns, ImmutableArray<string> roots,
        out string matchedRoot, out string feature, out string internalRoot)
    {
        matchedRoot = feature = internalRoot = string.Empty;

        foreach (var root in roots)
        {
            // Expect format: {root}.{feature}.Internal[.anything]
            if (!ns.StartsWith(root + "."))
                continue;

            var remainder = ns.Substring(root.Length + 1); // after "root."
            var firstDot = remainder.IndexOf('.');
            if (firstDot < 0) // no feature segment
                continue;

            feature = remainder.Substring(0, firstDot); // Feature1
            var afterFeature = remainder.Substring(firstDot + 1); // maybe "Internal"...
            if (!afterFeature.StartsWith("Internal"))
                continue;

            // Must be ".Internal" or ".Internal."
            if (afterFeature.Length > "Internal".Length && afterFeature["Internal".Length] != '.')
                continue;

            matchedRoot = root;
            internalRoot = $"{root}.{feature}.Internal";
            return true;
        }

        return false;
    }

    private static string GetAccessorNamespace(SyntaxNodeAnalysisContext context)
    {
        // 1) From the analyzer’s containing symbol
        var sym = context.ContainingSymbol;
        var ns = GetNamespaceFromSymbol(sym);
        if (!string.IsNullOrEmpty(ns))
            return ns;

        // 2) From the semantic model’s enclosing symbol at the current position
        var enclosing = context.SemanticModel.GetEnclosingSymbol(context.Node.SpanStart, context.CancellationToken);
        ns = GetNamespaceFromSymbol(enclosing);
        if (!string.IsNullOrEmpty(ns))
        {
            return ns;
        }

        // 3) From syntax: nearest (file-scoped or block-scoped) namespace declaration
        var nsDecl = context.Node.AncestorsAndSelf().OfType<BaseNamespaceDeclarationSyntax>().FirstOrDefault();
        if (nsDecl is not null)
        {
            return nsDecl.Name.ToString(); // handles qualified names too
        }

        // 4) Global namespace (no namespace)
        return string.Empty;

        static string GetNamespaceFromSymbol(ISymbol? s)
        {
            if (s is null)
            {
                return string.Empty;
                
            }

            // If the symbol itself is a namespace, use it
            if (s is INamespaceSymbol nsSym)
            {
                return nsSym.IsGlobalNamespace ? string.Empty : nsSym.ToDisplayString();
            }

            // Otherwise use the containing namespace
            var cns = s.ContainingNamespace;
            return cns is { IsGlobalNamespace: false } ? cns.ToDisplayString() : string.Empty;
        }
    }

    /// <summary>
    /// True iff accessorNs is inside the same feature root: {root}.{feature}(.anything)
    /// </summary>
    private static bool IsWithinFeature(string accessorNs, string root, string feature)
    {
        if (string.IsNullOrEmpty(accessorNs))
        {
            return false;
        }

        var featureRoot = $"{root}.{feature}";
        if (accessorNs == featureRoot)
        {
            return true;
        }

        if (accessorNs.StartsWith(featureRoot + "."))
        {
            return true;
        }

        return false;
    }

    private readonly record struct AnalyzerConfig(ImmutableArray<string> Roots, bool IsTestProject)
    {
        public static AnalyzerConfig Read(SyntaxNodeAnalysisContext context)
        {
            var provider = context.Options.AnalyzerConfigOptionsProvider;
            var tree = context.Node.SyntaxTree;
            var treeOptions = provider.GetOptions(tree);
            var keys = treeOptions.Keys.ToList();
            
            // dotnet_code_quality.stackworx.analyzers.namespaceinternal.root_namespaces
            // 1) Preferred: dotnet_code_quality.NamespaceInternal.root_namespaces
            var roots = ReadMulti(treeOptions, "dotnet_code_quality.stackworx.analyzers.NamespaceInternal.root_namespaces");

            // 2) Fallback (alternate key): dotnet_diagnostic.NSINT.root_namespaces
            if (roots.IsDefaultOrEmpty)
            {
                roots = ReadMulti(treeOptions, "dotnet_diagnostic.NSINT.root_namespaces");
            }

            // 3) Single root fallback: dotnet_code_quality.NamespaceInternal.root_namespace
            if (roots.IsDefaultOrEmpty)
            {
                var single = ReadSingle(treeOptions, "dotnet_code_quality.NamespaceInternal.root_namespace");
                if (!string.IsNullOrWhiteSpace(single))
                    roots = ImmutableArray.Create(single!);
            }

            // Test exemption
            var isTest = ReadSingle(treeOptions, "build_property.IsTestProject");
            var isTestProject = string.Equals(isTest, "true", System.StringComparison.OrdinalIgnoreCase);

            return new AnalyzerConfig(Normalize(roots), isTestProject);

            static ImmutableArray<string> ReadMulti(AnalyzerConfigOptions o, string key)
            {
                if (o.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value))
                {
                    var items = value.Split(';')
                        .Select(s => s.Trim())
                        .Where(s => !string.IsNullOrWhiteSpace(s))
                        .ToArray();
                    return items.Length > 0 ? ImmutableArray.Create(items) : default;
                }
                return default;
            }

            static string? ReadSingle(AnalyzerConfigOptions o, string key)
            {
                return o.TryGetValue(key, out var value) ? value.Trim() : null;
            }

            static ImmutableArray<string> Normalize(ImmutableArray<string> arr)
            {
                if (arr.IsDefaultOrEmpty) return arr;
                return arr.Select(s => s.TrimEnd('.')).Distinct().ToImmutableArray();
            }
        }
    }

    private record Violation
    {
        public required ExpressionSyntax Node { get; init; }
        
        public required string? TargetNs { get; init; }
        
        public required string Feature { get; init; }
        
        public required string InternalRoot { get; init; }
    }
}