namespace Stackworx.Analyzers.Tests;

using System.Threading.Tasks;
using Xunit;
using Verifier =
    Microsoft.CodeAnalysis.CSharp.Testing.CSharpAnalyzerVerifier<
        Stackworx.Analyzers.GraphQLUnusedDataLoaderAnalyzer,
        Microsoft.CodeAnalysis.Testing.DefaultVerifier>;

public class UnusedDataLoaderAnalyzerTests
{
    [Fact]
    public async Task Reports_WhenIDataLoaderInterfaceIsUnused()
    {
        const string testCode = @"
using System;

namespace GreenDonut
{
    public interface IDataLoader<TKey, TValue> { }
}

public record Author;

public interface {|#0:MyDataLoader|}
    : GreenDonut.IDataLoader<Guid, Author>
{
}
";

        var expected = Verifier.Diagnostic(GraphQLUnusedDataLoaderAnalyzer.UnusedDataLoaderInterfaceRule)
            .WithArguments("MyDataLoader")
            .WithLocation(0);

        await Verifier.VerifyAnalyzerAsync(testCode, expected);
    }

    [Fact]
    public async Task DoesNotReport_WhenIDataLoaderInterfaceIsReferenced()
    {
        const string testCode = @"
using System;

namespace GreenDonut
{
    public interface IDataLoader<TKey, TValue> { }
}

public record Author;

public interface IMyDataLoader
    : GreenDonut.IDataLoader<Guid, Author>
{
}

public class UsesLoader
{
    private IMyDataLoader _loader; // reference => considered used
}
";
        await Verifier.VerifyAnalyzerAsync(testCode);
    }

    [Fact]
    public async Task ReportsOnlyTheUnusedInterface_WhenMixedUsedAndUnused()
    {
        const string testCode = @"
using System;

namespace GreenDonut
{
    public interface IDataLoader<TKey, TValue> { }
}

public record Author;
public record Book;

public interface {|#0:IUnusedLoader|}
    : GreenDonut.IDataLoader<Guid, Author> { }

public interface IUsedLoader
    : GreenDonut.IDataLoader<Guid, Book> { }

public class UsesLoader
{
    private IUsedLoader _loader; // Only this one is referenced
}
";

        var expected = Verifier.Diagnostic(GraphQLUnusedDataLoaderAnalyzer.UnusedDataLoaderInterfaceRule)
            .WithArguments("IUnusedLoader")
            .WithLocation(0);

        await Verifier.VerifyAnalyzerAsync(testCode, expected);
    }
}