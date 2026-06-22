namespace Stackworx.Analyzers.Fixes;

using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Editing;

internal static class StaticModifierSyntaxHelper
{
    /// <summary>
    /// Returns a copy of <paramref name="classDecl"/> marked <c>static</c>, with every
    /// instance method also marked <c>static</c>. A static class can only contain static
    /// members, so promoting the methods keeps the result compilable.
    /// </summary>
    /// <remarks>
    /// Descendant methods are rewritten first, then the class itself, then the caller does a
    /// single <c>ReplaceNode</c> in the root. Editing the class and its methods as separate
    /// edits in one batch would hit the ancestor/descendant conflict a <c>DocumentEditor</c>
    /// throws on.
    /// </remarks>
    public static ClassDeclarationSyntax MakeClassAndMethodsStatic(
        SyntaxGenerator generator,
        ClassDeclarationSyntax classDecl)
    {
        var instanceMethods = classDecl.Members
            .OfType<MethodDeclarationSyntax>()
            .Where(m => !m.Modifiers.Any(t => t.IsKind(SyntaxKind.StaticKeyword)))
            .ToArray();

        var updated = classDecl;
        if (instanceMethods.Length > 0)
        {
            updated = classDecl.ReplaceNodes(
                instanceMethods,
                (_, rewritten) => (MethodDeclarationSyntax)WithStatic(generator, rewritten));
        }

        return (ClassDeclarationSyntax)WithStatic(generator, updated);
    }

    private static SyntaxNode WithStatic(SyntaxGenerator generator, SyntaxNode node) =>
        generator.WithModifiers(node, generator.GetModifiers(node).WithIsStatic(true));
}
