namespace Stackworx.Analyzers.Tests;

using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Xunit;
using Verifier =
    Microsoft.CodeAnalysis.CSharp.Testing.CSharpAnalyzerVerifier<
        Stackworx.Analyzers.GraphQLStaticExtensionClassAnalyzer,
        Microsoft.CodeAnalysis.Testing.DefaultVerifier>;

public class GraphQLStaticExtensionClassAnalyzerTests
{
    [Fact]
    public async Task ReportsError_NonStaticClass()
    {
        const string testCode = @"
using System;
using System.Threading;
using System.Threading.Tasks;
using HotChocolate;

namespace HotChocolate
{
    [System.AttributeUsage(System.AttributeTargets.Class)]
    public sealed class ExtendObjectTypeAttribute<T> : System.Attribute { }
}

public record Author;

[ExtendObjectType<Author>]
public class {|#0:QueryResolvers|}
{
}
";

        var expected = Verifier.Diagnostic(GraphQLStaticExtensionClassAnalyzer.Rule)
            // .WithMessage("Field extension method 'GetAuthorAsync' cannot be static. Make it an instance method.")
            .WithArguments("QueryResolvers")
            .WithLocation(0)
            .WithSeverity(DiagnosticSeverity.Error);

        await Verifier.VerifyAnalyzerAsync(testCode, expected);
    }
    
    [Fact]
    public async Task NoError_StaticClass()
    {
        const string testCode = @"
using System;
using System.Threading;
using System.Threading.Tasks;
using HotChocolate;

namespace HotChocolate
{
    [System.AttributeUsage(System.AttributeTargets.Class)]
    public sealed class ExtendObjectTypeAttribute<T> : System.Attribute { }
}

public record Author;

[ExtendObjectType<Author>]
public static class QueryResolvers
{
}
";

        // No Errors
        await Verifier.VerifyAnalyzerAsync(testCode);
    }
}