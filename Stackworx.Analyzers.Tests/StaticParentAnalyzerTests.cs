namespace Stackworx.Analyzers.Tests;

using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Xunit;
using Verifier =
    Microsoft.CodeAnalysis.CSharp.Testing.CSharpAnalyzerVerifier<
        Stackworx.Analyzers.GraphQLStaticExtensionMethodAnalyzer,
        Microsoft.CodeAnalysis.Testing.DefaultVerifier>;

public class StaticParentAnalyzerTests
{
    [Fact]
    public async Task ReportsError_WhenStaticMethodHasParentParameter()
    {
        const string testCode = @"
using System;
using System.Threading;
using System.Threading.Tasks;
using HotChocolate;

namespace HotChocolate
{
    [System.AttributeUsage(System.AttributeTargets.Parameter)]
    public sealed class ParentAttribute : System.Attribute { }
}

public record Author;
public record Book;

public static class QueryResolvers
{
    public static async Task<Author> {|#0:GetAuthorAsync|}(
        [Parent] Book parent,
        CancellationToken ct)
    {
        // not relevant
        return null!;
    }
}
";

        var expected = Verifier.Diagnostic(GraphQLStaticExtensionMethodAnalyzer.ParentOnStaticMethodRule)
            // .WithMessage("Field extension method 'GetAuthorAsync' cannot be static. Make it an instance method.")
            .WithArguments("GetAuthorAsync")
            .WithLocation(0)
            
            .WithSeverity(DiagnosticSeverity.Error);

        await Verifier.VerifyAnalyzerAsync(testCode, expected);
    }
    
    [Fact]
    public async Task NoError_WhenInstanceMethodHasParentParameter()
    {
        const string testCode = @"
using System;
using System.Threading;
using System.Threading.Tasks;
using HotChocolate;

namespace HotChocolate
{
    [System.AttributeUsage(System.AttributeTargets.Parameter)]
    public sealed class ParentAttribute : System.Attribute { }
}

public record Author;
public record Book;

public class QueryResolvers
{
    public async Task<Author> GetAuthorAsync(
        [{|#0:Parent|}] Book parent,
        CancellationToken ct)
    {
        throw new NotImplementedException();
    }
}
";

        // No Errors
        await Verifier.VerifyAnalyzerAsync(testCode);
    }
}