namespace Stackworx.Analyzers.Fixes;

using System.Collections.Immutable;
using System.Composition;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Editing;
using Microsoft.CodeAnalysis.Formatting;

[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(GraphQLStaticExtensionClassCodeFixProvider))]
[Shared]
public sealed class GraphQLStaticExtensionClassCodeFixProvider : CodeFixProvider
{
    public override ImmutableArray<string> FixableDiagnosticIds =>
        ImmutableArray.Create(GraphQLStaticExtensionClassAnalyzer.Rule.Id);

    public override FixAllProvider GetFixAllProvider() => WellKnownFixAllProviders.BatchFixer;

    public override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        var diagnostic = context.Diagnostics.FirstOrDefault();
        if (diagnostic is null)
        {
            return;
        }

        var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
        if (root is null)
        {
            return;
        }

        var node = root.FindNode(diagnostic.Location.SourceSpan, getInnermostNodeForTie: true);
        var classDecl = node.FirstAncestorOrSelf<ClassDeclarationSyntax>();
        if (classDecl is null)
        {
            return;
        }

        context.RegisterCodeFix(
            CodeAction.Create(
                title: "Make class static",
                createChangedDocument: c => MakeStaticAsync(context.Document, classDecl, c),
                equivalenceKey: "MakeClassStatic"),
            diagnostic);
    }

    private static async Task<Document> MakeStaticAsync(
        Document document,
        ClassDeclarationSyntax classDecl,
        CancellationToken cancellationToken)
    {
        var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
        if (root is null)
        {
            return document;
        }

        var generator = SyntaxGenerator.GetGenerator(document);
        var newClass = StaticModifierSyntaxHelper.MakeClassAndMethodsStatic(generator, classDecl)
            .WithAdditionalAnnotations(Formatter.Annotation);

        var newRoot = root.ReplaceNode(classDecl, newClass);
        return document.WithSyntaxRoot(newRoot);
    }
}
