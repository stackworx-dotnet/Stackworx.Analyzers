namespace Stackworx.Analyzers.Tests;

using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Xunit;
using Verifier =
    Microsoft.CodeAnalysis.CSharp.Testing.CSharpCodeFixVerifier<
        Stackworx.Analyzers.GraphQLStaticExtensionClassAnalyzer,
        Fixes.GraphQLStaticExtensionClassCodeFixProvider,
        Microsoft.CodeAnalysis.Testing.DefaultVerifier>;

public class GraphQLStaticExtensionClassCodeFixProviderTests
{
    [Fact]
    public async Task MakesClassAndAllMethodsStatic()
    {
        const string before = @"
using System.Threading;
using System.Threading.Tasks;

namespace HotChocolate
{
    [System.AttributeUsage(System.AttributeTargets.Class)]
    public sealed class ExtendObjectTypeAttribute<T> : System.Attribute { }

    [System.AttributeUsage(System.AttributeTargets.Parameter)]
    public sealed class ParentAttribute : System.Attribute { }
}

public record Author;
public record Book;

[HotChocolate.ExtendObjectType<Author>]
public class {|#0:QueryResolvers|}
{
    public async Task<Author> GetAuthorAsync([HotChocolate.Parent] Book parent, CancellationToken ct)
    {
        return null!;
    }

    public int Count() => 0;
}
";

        const string after = @"
using System.Threading;
using System.Threading.Tasks;

namespace HotChocolate
{
    [System.AttributeUsage(System.AttributeTargets.Class)]
    public sealed class ExtendObjectTypeAttribute<T> : System.Attribute { }

    [System.AttributeUsage(System.AttributeTargets.Parameter)]
    public sealed class ParentAttribute : System.Attribute { }
}

public record Author;
public record Book;

[HotChocolate.ExtendObjectType<Author>]
public static class QueryResolvers
{
    public static async Task<Author> GetAuthorAsync([HotChocolate.Parent] Book parent, CancellationToken ct)
    {
        return null!;
    }

    public static int Count() => 0;
}
";

        var expected = Verifier.Diagnostic(GraphQLStaticExtensionClassAnalyzer.Rule)
            .WithArguments("QueryResolvers")
            .WithLocation(0)
            .WithSeverity(DiagnosticSeverity.Error);

        await Verifier.VerifyCodeFixAsync(before, expected, after);
    }
}
