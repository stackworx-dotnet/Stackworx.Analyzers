namespace Stackworx.Analyzers.Fixes;

using System;
using System.Collections.Immutable;
using System.Composition;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Editing;
using Microsoft.CodeAnalysis.Formatting;
using Microsoft.CodeAnalysis.Simplification;

[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(GraphQLHotChocolateTypeUsedImplicitlyCodeFixProvider))]
[Shared]
public sealed class GraphQLHotChocolateTypeUsedImplicitlyCodeFixProvider : CodeFixProvider
{
    public override ImmutableArray<string> FixableDiagnosticIds =>
        [GraphQLHotChocolateTypeUsedImplicitlyAnalyzer.MissingUsedImplicitlyRule.Id];

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
        var typeDecl = node.FirstAncestorOrSelf<TypeDeclarationSyntax>();
        if (typeDecl is null)
        {
            return;
        }

        context.RegisterCodeFix(
            CodeAction.Create(
                title: "Add [UsedImplicitly]",
                createChangedDocument: c => AddUsedImplicitlyAsync(context.Document, typeDecl, c),
                equivalenceKey: "AddUsedImplicitly"),
            diagnostic);
    }

    private static async Task<Document> AddUsedImplicitlyAsync(
        Document document,
        TypeDeclarationSyntax typeDecl,
        CancellationToken cancellationToken)
    {
        // If it's already there (maybe the diagnostic is stale), do nothing.
        if (typeDecl.AttributeLists.SelectMany(a => a.Attributes)
            .Any(a => a.Name.ToString().Contains("UsedImplicitly", StringComparison.Ordinal)))
        {
            return document;
        }

        var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
        if (root is null)
        {
            return document;
        }

        // Add the attribute fully qualified and annotated. ImportAdder then inserts the
        // matching `using JetBrains.Annotations;` in the file's preferred location
        // (respecting file-scoped namespaces and `csharp_using_directive_placement`, so
        // it won't trip StyleCop SA1200), and Simplifier shortens the name back down to
        // `[UsedImplicitly]`.
        var attributeList = SyntaxFactory.AttributeList(
                SyntaxFactory.SingletonSeparatedList(
                    SyntaxFactory.Attribute(SyntaxFactory.ParseName("JetBrains.Annotations.UsedImplicitly"))))
            .WithTrailingTrivia(SyntaxFactory.ElasticLineFeed)
            .WithAdditionalAnnotations(Simplifier.Annotation, Formatter.Annotation);

        var newTypeDecl = typeDecl.AddAttributeLists(attributeList);
        var newDocument = document.WithSyntaxRoot(root.ReplaceNode(typeDecl, newTypeDecl));

        newDocument = await ImportAdder
            .AddImportsAsync(newDocument, Simplifier.Annotation, cancellationToken: cancellationToken)
            .ConfigureAwait(false);
        newDocument = await Simplifier
            .ReduceAsync(newDocument, Simplifier.Annotation, cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        return newDocument;
    }
}
