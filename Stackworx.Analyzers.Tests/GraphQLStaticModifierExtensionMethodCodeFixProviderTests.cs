namespace Stackworx.Analyzers.Tests;

using System.Threading.Tasks;
using Microsoft.CodeAnalysis.Testing;
using Xunit;
using CodeFixTest =
    Microsoft.CodeAnalysis.CSharp.Testing.CSharpCodeFixTest<
        Stackworx.Analyzers.GraphQLStaticModifierExtensionMethodAnalyzer,
        Stackworx.Analyzers.Fixes.GraphQLStaticModifierExtensionMethodCodeFixProvider,
        Microsoft.CodeAnalysis.Testing.DefaultVerifier>;

public class GraphQLStaticModifierExtensionMethodCodeFixProviderTests
{
    private const string Before = @"
using System.Threading;
using System.Threading.Tasks;

namespace HotChocolate
{
    [System.AttributeUsage(System.AttributeTargets.Parameter)]
    public sealed class ParentAttribute : System.Attribute { }
}

public record Author;
public record Book;

public class QueryResolvers
{
    public static async Task<Author> {|#0:GetAuthorAsync|}([HotChocolate.Parent] Book parent, CancellationToken ct)
    {
        return null!;
    }
}
";

    [Fact]
    public async Task MakeMethodInstance_RemovesStaticFromMethod()
    {
        const string after = @"
using System.Threading;
using System.Threading.Tasks;

namespace HotChocolate
{
    [System.AttributeUsage(System.AttributeTargets.Parameter)]
    public sealed class ParentAttribute : System.Attribute { }
}

public record Author;
public record Book;

public class QueryResolvers
{
    public async Task<Author> GetAuthorAsync([HotChocolate.Parent] Book parent, CancellationToken ct)
    {
        return null!;
    }
}
";

        var test = new CodeFixTest
        {
            TestCode = Before,
            FixedCode = after,
            CodeActionEquivalenceKey = "MakeMethodInstance",
        };
        test.ExpectedDiagnostics.Add(
            new DiagnosticResult(GraphQLStaticModifierExtensionMethodAnalyzer.StaticMethodInNonStaticClassRule)
                .WithLocation(0)
                .WithArguments("GetAuthorAsync", "QueryResolvers"));

        await test.RunAsync();
    }

    [Fact]
    public async Task MakeContainingClassStatic_MakesClassAndMethodsStatic()
    {
        const string after = @"
using System.Threading;
using System.Threading.Tasks;

namespace HotChocolate
{
    [System.AttributeUsage(System.AttributeTargets.Parameter)]
    public sealed class ParentAttribute : System.Attribute { }
}

public record Author;
public record Book;

public static class QueryResolvers
{
    public static async Task<Author> GetAuthorAsync([HotChocolate.Parent] Book parent, CancellationToken ct)
    {
        return null!;
    }
}
";

        var test = new CodeFixTest
        {
            TestCode = Before,
            FixedCode = after,
            CodeActionEquivalenceKey = "MakeContainingClassStatic",
        };
        test.ExpectedDiagnostics.Add(
            new DiagnosticResult(GraphQLStaticModifierExtensionMethodAnalyzer.StaticMethodInNonStaticClassRule)
                .WithLocation(0)
                .WithArguments("GetAuthorAsync", "QueryResolvers"));

        await test.RunAsync();
    }
}
