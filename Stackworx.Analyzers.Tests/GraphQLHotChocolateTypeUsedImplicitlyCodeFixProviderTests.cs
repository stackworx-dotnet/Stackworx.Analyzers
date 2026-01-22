namespace Stackworx.Analyzers.Tests;

using System.Threading.Tasks;
using Xunit;
using Verifier =
    Microsoft.CodeAnalysis.CSharp.Testing.CSharpCodeFixVerifier<
        Stackworx.Analyzers.GraphQLHotChocolateTypeUsedImplicitlyAnalyzer,
        Fixes.GraphQLHotChocolateTypeUsedImplicitlyCodeFixProvider,
        Microsoft.CodeAnalysis.Testing.DefaultVerifier>;

public class GraphQLHotChocolateTypeUsedImplicitlyCodeFixProviderTests
{
    [Fact(Skip = "Not Working")]
    public async Task AddsUsedImplicitlyAttributeAndUsing_ForQueryType()
    {
        const string before = @"
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

        const string after = @"
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

using JetBrains.Annotations;

[HotChocolate.Types.QueryType]
[UsedImplicitly]
public class MyQuery
{
}
";

        var expected = Verifier.Diagnostic(GraphQLHotChocolateTypeUsedImplicitlyAnalyzer.MissingUsedImplicitlyRule)
            .WithLocation(0)
            .WithArguments("MyQuery");

        await Verifier.VerifyCodeFixAsync(before, expected, after);
    }
}

