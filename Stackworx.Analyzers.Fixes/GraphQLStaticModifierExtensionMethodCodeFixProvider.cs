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

[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(GraphQLStaticModifierExtensionMethodCodeFixProvider))]
[Shared]
public sealed class GraphQLStaticModifierExtensionMethodCodeFixProvider : CodeFixProvider
{
    public override ImmutableArray<string> FixableDiagnosticIds =>
        ImmutableArray.Create(GraphQLStaticModifierExtensionMethodAnalyzer.StaticMethodInNonStaticClassRule.Id);

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
        var methodDecl = node.FirstAncestorOrSelf<MethodDeclarationSyntax>();
        if (methodDecl is null)
        {
            return;
        }

        // Primary fix, matching the analyzer message: drop `static` so the resolver
        // becomes an instance method.
        context.RegisterCodeFix(
            CodeAction.Create(
                title: "Make method an instance method",
                createChangedDocument: c => MakeMethodInstanceAsync(context.Document, methodDecl, c),
                equivalenceKey: "MakeMethodInstance"),
            diagnostic);

        // Alternative fix: make the containing class static instead (and promote its
        // other methods so the class still compiles).
        var classDecl = methodDecl.FirstAncestorOrSelf<ClassDeclarationSyntax>();
        if (classDecl is not null)
        {
            context.RegisterCodeFix(
                CodeAction.Create(
                    title: "Make containing class static",
                    createChangedDocument: c => MakeClassStaticAsync(context.Document, classDecl, c),
                    equivalenceKey: "MakeContainingClassStatic"),
                diagnostic);
        }
    }

    private static async Task<Document> MakeMethodInstanceAsync(
        Document document,
        MethodDeclarationSyntax methodDecl,
        CancellationToken cancellationToken)
    {
        var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
        if (root is null)
        {
            return document;
        }

        var generator = SyntaxGenerator.GetGenerator(document);
        var newMethod = generator.WithModifiers(
                methodDecl,
                generator.GetModifiers(methodDecl).WithIsStatic(false))
            .WithAdditionalAnnotations(Formatter.Annotation);

        var newRoot = root.ReplaceNode(methodDecl, newMethod);
        return document.WithSyntaxRoot(newRoot);
    }

    private static async Task<Document> MakeClassStaticAsync(
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
