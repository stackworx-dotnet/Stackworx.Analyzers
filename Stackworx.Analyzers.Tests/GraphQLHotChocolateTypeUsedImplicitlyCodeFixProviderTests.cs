namespace Stackworx.Analyzers.Tests;

using System.Threading.Tasks;
using Microsoft.CodeAnalysis.Testing;
using Xunit;
using Verifier =
    Microsoft.CodeAnalysis.CSharp.Testing.CSharpCodeFixVerifier<
        Stackworx.Analyzers.GraphQLHotChocolateTypeUsedImplicitlyAnalyzer,
        Fixes.GraphQLHotChocolateTypeUsedImplicitlyCodeFixProvider,
        Microsoft.CodeAnalysis.Testing.DefaultVerifier>;
using FixTest =
    Microsoft.CodeAnalysis.CSharp.Testing.CSharpCodeFixTest<
        Stackworx.Analyzers.GraphQLHotChocolateTypeUsedImplicitlyAnalyzer,
        Stackworx.Analyzers.Fixes.GraphQLHotChocolateTypeUsedImplicitlyCodeFixProvider,
        Microsoft.CodeAnalysis.Testing.DefaultVerifier>;

public class GraphQLHotChocolateTypeUsedImplicitlyCodeFixProviderTests
{
    [Fact]
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
using JetBrains.Annotations;

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

    [Fact]
    public async Task AddsUsingAndAttribute_FileScopedNamespaceWithExistingUsings()
    {
        const string stubs = @"
namespace JetBrains.Annotations
{
    [System.AttributeUsage(System.AttributeTargets.All, AllowMultiple = true)]
    public sealed class UsedImplicitlyAttribute : System.Attribute { }
}

namespace HotChocolate.Types
{
    [System.AttributeUsage(System.AttributeTargets.Class)]
    public sealed class ExtendObjectTypeAttribute : System.Attribute
    {
        public ExtendObjectTypeAttribute(System.Type type) { }
    }
}
";

        const string before = @"
namespace App.Resolvers;

using HotChocolate.Types;

[ExtendObjectType(typeof(string))]
public class {|#0:UserExtensions|}
{
}
";

        // ImportAdder inserts `using JetBrains.Annotations;` alongside the existing
        // using (inside the file-scoped namespace, not above it) and Simplifier shortens
        // the attribute to `[UsedImplicitly]`.
        const string after = @"
namespace App.Resolvers;

using HotChocolate.Types;
using JetBrains.Annotations;

[ExtendObjectType(typeof(string))]
[UsedImplicitly]
public class UserExtensions
{
}
";

        var test = new FixTest
        {
            TestState =
            {
                Sources = { before, stubs },
                ExpectedDiagnostics =
                {
                    Verifier.Diagnostic(GraphQLHotChocolateTypeUsedImplicitlyAnalyzer.MissingUsedImplicitlyRule)
                        .WithLocation(0)
                        .WithArguments("UserExtensions"),
                },
            },
            FixedState =
            {
                Sources = { after, stubs },
            },
        };

        await test.RunAsync();
    }
}

