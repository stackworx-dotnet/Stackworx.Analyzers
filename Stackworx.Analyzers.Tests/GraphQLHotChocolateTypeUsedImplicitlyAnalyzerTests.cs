namespace Stackworx.Analyzers.Tests;

using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Xunit;
using Verifier =
    Microsoft.CodeAnalysis.CSharp.Testing.CSharpAnalyzerVerifier<
        Stackworx.Analyzers.GraphQLHotChocolateTypeUsedImplicitlyAnalyzer,
        Microsoft.CodeAnalysis.Testing.DefaultVerifier>;

public class GraphQLHotChocolateTypeUsedImplicitlyAnalyzerTests
{
    [Fact]
    public async Task Reports_WhenHotChocolateQueryTypeMissingUsedImplicitly()
    {
        const string testCode = @"
namespace JetBrains.Annotations
{
    [System.AttributeUsage(System.AttributeTargets.All, AllowMultiple = true)]
    public sealed class UsedImplicitlyAttribute : System.Attribute { }
}

namespace HotChocolate.Types
{
    [System.AttributeUsage(System.AttributeTargets.Class)]
    public sealed class QueryTypeAttribute : System.Attribute { }
}

[HotChocolate.Types.QueryType]
public class {|#0:MyQuery|}
{
}
";

        var expected = Verifier.Diagnostic(GraphQLHotChocolateTypeUsedImplicitlyAnalyzer.MissingUsedImplicitlyRule)
            .WithArguments("MyQuery")
            .WithLocation(0)
            .WithSeverity(DiagnosticSeverity.Warning);

        await Verifier.VerifyAnalyzerAsync(testCode, expected);
    }

    [Fact]
    public async Task DoesNotReport_WhenHotChocolateQueryTypeHasUsedImplicitly()
    {
        const string testCode = @"
namespace JetBrains.Annotations
{
    [System.AttributeUsage(System.AttributeTargets.All, AllowMultiple = true)]
    public sealed class UsedImplicitlyAttribute : System.Attribute { }
}

namespace HotChocolate.Types
{
    [System.AttributeUsage(System.AttributeTargets.Class)]
    public sealed class QueryTypeAttribute : System.Attribute { }
}

[JetBrains.Annotations.UsedImplicitly]
[HotChocolate.Types.QueryType]
public class MyQuery
{
}
";

        await Verifier.VerifyAnalyzerAsync(testCode);
    }

    [Fact]
    public async Task Reports_WhenUsedImplicitlyButNotAHotChocolateGraphQlType()
    {
        const string testCode = @"
namespace JetBrains.Annotations
{
    [System.AttributeUsage(System.AttributeTargets.All, AllowMultiple = true)]
    public sealed class UsedImplicitlyAttribute : System.Attribute { }
}

[JetBrains.Annotations.UsedImplicitly]
public class {|#0:SomeUtility|}
{
}
";

        var expected = Verifier.Diagnostic(GraphQLHotChocolateTypeUsedImplicitlyAnalyzer.UsedImplicitlyOnNonGraphQLTypeRule)
            .WithArguments("SomeUtility")
            .WithLocation(0)
            .WithSeverity(DiagnosticSeverity.Warning);

        await Verifier.VerifyAnalyzerAsync(testCode, expected);
    }

    [Fact]
    public async Task Reports_WhenDerivedFromObjectTypeMissingUsedImplicitly()
    {
        const string testCode = @"
namespace JetBrains.Annotations
{
    [System.AttributeUsage(System.AttributeTargets.All, AllowMultiple = true)]
    public sealed class UsedImplicitlyAttribute : System.Attribute { }
}

namespace HotChocolate.Types
{
    public class ObjectType { }
}

public class {|#0:MyObjectType|} : HotChocolate.Types.ObjectType
{
}
";

        var expected = Verifier.Diagnostic(GraphQLHotChocolateTypeUsedImplicitlyAnalyzer.MissingUsedImplicitlyRule)
            .WithArguments("MyObjectType")
            .WithLocation(0)
            .WithSeverity(DiagnosticSeverity.Warning);

        await Verifier.VerifyAnalyzerAsync(testCode, expected);
    }
}

