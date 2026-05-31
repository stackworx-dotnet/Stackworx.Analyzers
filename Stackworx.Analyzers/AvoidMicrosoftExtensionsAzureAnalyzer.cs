namespace Stackworx.Analyzers;

using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class AvoidMicrosoftExtensionsAzureAnalyzer : DiagnosticAnalyzer
{
    public const string DiagnosticId = "SW103";

    private const string ForbiddenNamespace = "Microsoft.Extensions.Azure";
    private const string Category = "Architecture";

    private static readonly LocalizableString Title =
        "Avoid Microsoft.Extensions.Azure usage";

    private static readonly LocalizableString MessageFormat =
        "Usage of Microsoft.Extensions.Azure is discouraged; migrate to keyed service registrations";

    private static readonly LocalizableString Description =
        "Microsoft.Extensions.Azure registrations should be replaced by keyed service registrations.";

    public static readonly DiagnosticDescriptor Rule = new(
        id: DiagnosticId,
        title: Title,
        messageFormat: MessageFormat,
        category: Category,
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: Description);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule);

    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

        context.RegisterSyntaxNodeAction(AnalyzeUsingDirective, Microsoft.CodeAnalysis.CSharp.SyntaxKind.UsingDirective);
        context.RegisterOperationAction(AnalyzeOperation, OperationKind.Invocation, OperationKind.ObjectCreation);
    }

    private static void AnalyzeUsingDirective(SyntaxNodeAnalysisContext context)
    {
        var usingDirective = (UsingDirectiveSyntax)context.Node;
        var namespaceText = usingDirective.Name?.ToString();
        if (namespaceText is null)
        {
            return;
        }

        if (namespaceText == ForbiddenNamespace || namespaceText.StartsWith(ForbiddenNamespace + ".", System.StringComparison.Ordinal))
        {
            context.ReportDiagnostic(Diagnostic.Create(Rule, usingDirective.Name!.GetLocation()));
        }
    }

    private static void AnalyzeOperation(OperationAnalysisContext context)
    {
        ISymbol? symbol = context.Operation switch
        {
            IInvocationOperation invocation => invocation.TargetMethod,
            IObjectCreationOperation objectCreation => objectCreation.Constructor,
            _ => null,
        };

        if (symbol is null || symbol.ContainingNamespace is null)
        {
            return;
        }

        var namespaceText = symbol.ContainingNamespace.ToDisplayString();
        if (namespaceText == ForbiddenNamespace || namespaceText.StartsWith(ForbiddenNamespace + ".", System.StringComparison.Ordinal))
        {
            context.ReportDiagnostic(Diagnostic.Create(Rule, context.Operation.Syntax.GetLocation()));
        }
    }
}
