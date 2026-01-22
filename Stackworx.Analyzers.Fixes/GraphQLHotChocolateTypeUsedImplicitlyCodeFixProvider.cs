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

[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(GraphQLHotChocolateTypeUsedImplicitlyCodeFixProvider))]
[Shared]
public sealed class GraphQLHotChocolateTypeUsedImplicitlyCodeFixProvider : CodeFixProvider
{
    public override ImmutableArray<string> FixableDiagnosticIds =>
        ImmutableArray.Create(GraphQLHotChocolateTypeUsedImplicitlyAnalyzer.MissingUsedImplicitlyRule.Id);

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

        var editor = await DocumentEditor.CreateAsync(document, cancellationToken).ConfigureAwait(false);

        // Add using JetBrains.Annotations; if not present and if we can.
        // For files with no namespace or regular namespace decls, this is safe.
        // For file-scoped namespaces, DocumentEditor still handles it.
        AddUsingIfMissing(editor, "JetBrains.Annotations");

        // Prefer short name once using is present.
        var usedImplicitlyAttr = SyntaxFactory.Attribute(SyntaxFactory.IdentifierName("UsedImplicitly"));

        var newTypeDecl = typeDecl.AddAttributeLists(
            SyntaxFactory.AttributeList(SyntaxFactory.SingletonSeparatedList(usedImplicitlyAttr))
                .WithTrailingTrivia(SyntaxFactory.ElasticCarriageReturnLineFeed));

        editor.ReplaceNode(typeDecl, newTypeDecl);

        // Ensure formatting.
        return editor.GetChangedDocument();
    }

    private static void AddUsingIfMissing(DocumentEditor editor, string namespaceName)
    {
        var root = editor.OriginalRoot as CompilationUnitSyntax;
        if (root is null)
        {
            return;
        }

        // Be defensive: treat both `using X;` and `using X = ...;` as already having the namespace.
        var exists = root.Usings.Any(u =>
            string.Equals(u.Name.ToString(), namespaceName, StringComparison.Ordinal));
        if (exists)
        {
            return;
        }

        // Add it to the compilation unit and let normal formatting place it correctly.
        var newRoot = root.AddUsings(
            SyntaxFactory.UsingDirective(SyntaxFactory.ParseName(namespaceName)));

        editor.ReplaceNode(root, newRoot);
    }
}
