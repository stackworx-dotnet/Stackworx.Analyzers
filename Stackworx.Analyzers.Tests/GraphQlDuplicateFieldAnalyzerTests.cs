namespace Stackworx.Analyzers.Tests;

using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Xunit;
using Verifier =
    Microsoft.CodeAnalysis.CSharp.Testing.CSharpAnalyzerVerifier<
        Stackworx.Analyzers.GraphQLDuplicateFieldAnalyzer,
        Microsoft.CodeAnalysis.Testing.DefaultVerifier>;

public class GraphQlDuplicateFieldAnalyzerTests
{
    [Fact]
    public async Task ReportsError_WhenDuplicateFieldForSameExtendedType()
    {
        const string testCode = @"
using System;
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

[HotChocolate.ExtendObjectType<Book>]
public static class BookExtensions1
{
    public static Task<Author> {|#1:GetAuthorAsync|}([HotChocolate.ParentAttribute] Book parent, CancellationToken ct)
        => Task.FromResult(new Author());
}

[HotChocolate.ExtendObjectType<Book>]
public static class BookExtensions2
{
    public static Task<Author> {|#0:Author|}([HotChocolate.ParentAttribute] Book parent, CancellationToken ct)
        => Task.FromResult(new Author());
}
";

        var expected = Verifier.Diagnostic(GraphQLDuplicateFieldAnalyzer.DuplicateFieldRule)
            .WithLocation(0)
            .WithLocation(1)
            .WithArguments("Author", "Book", "/0/Test0.cs")
            .WithSeverity(DiagnosticSeverity.Error);

        await Verifier.VerifyAnalyzerAsync(testCode, expected);
    }

    [Fact]
    public async Task NoError_WhenSameFieldNameButDifferentExtendedType()
    {
        const string testCode = @"
using System;
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
public record Magazine;

[HotChocolate.ExtendObjectType<Book>]
public static class BookExtensions
{
    public static Task<Author> GetAuthorAsync([HotChocolate.ParentAttribute] Book parent, CancellationToken ct)
        => Task.FromResult(new Author());
}

[HotChocolate.ExtendObjectType<Magazine>]
public static class MagazineExtensions
{
    public static Task<Author> GetAuthorAsync([HotChocolate.ParentAttribute] Magazine parent, CancellationToken ct)
        => Task.FromResult(new Author());
}
";

        await Verifier.VerifyAnalyzerAsync(testCode);
    }

    [Fact]
    public async Task NoError_WhenDifferentNormalizedNames()
    {
        const string testCode = @"
using System;
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

[HotChocolate.ExtendObjectType<Book>]
public static class BookExtensions
{
    public static Task<Author> GetAuthorAsync([HotChocolate.ParentAttribute] Book parent, CancellationToken ct)
        => Task.FromResult(new Author());

    public static Task<Author> GetAuthorsAsync([HotChocolate.ParentAttribute] Book parent, CancellationToken ct)
        => Task.FromResult(new Author());
}
";

        await Verifier.VerifyAnalyzerAsync(testCode);
    }

    [Fact]
    public async Task ReportsError_WhenDuplicateFieldIsInternal()
    {
        const string testCode = @"
using System;
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

[HotChocolate.ExtendObjectType<Book>]
public static class BookExtensions1
{
    internal static Task<Author> {|#1:GetAuthorAsync|}([HotChocolate.ParentAttribute] Book parent, CancellationToken ct)
        => Task.FromResult(new Author());
}

[HotChocolate.ExtendObjectType<Book>]
public static class BookExtensions2
{
    internal static Task<Author> {|#0:Author|}([HotChocolate.ParentAttribute] Book parent, CancellationToken ct)
        => Task.FromResult(new Author());
}
";

        var expected = Verifier.Diagnostic(GraphQLDuplicateFieldAnalyzer.DuplicateFieldRule)
            .WithLocation(0)
            .WithLocation(1)
            .WithArguments("Author", "Book", "/0/Test0.cs")
            .WithSeverity(DiagnosticSeverity.Error);

        await Verifier.VerifyAnalyzerAsync(testCode, expected);
    }

    [Fact]
    public async Task NoError_WhenDuplicateMethodIsGraphQLIgnored()
    {
        const string testCode = @"
using System;
using System.Threading;
using System.Threading.Tasks;

namespace HotChocolate
{
    [System.AttributeUsage(System.AttributeTargets.Class)]
    public sealed class ExtendObjectTypeAttribute<T> : System.Attribute { }

    [System.AttributeUsage(System.AttributeTargets.Parameter)]
    public sealed class ParentAttribute : System.Attribute { }

    [System.AttributeUsage(System.AttributeTargets.Method | System.AttributeTargets.Property | System.AttributeTargets.Field)]
    public sealed class GraphQLIgnoreAttribute : System.Attribute { }
}

public record Author;
public record Book;

[HotChocolate.ExtendObjectType<Book>]
public static class BookExtensions1
{
    public static Task<Author> GetAuthorAsync([HotChocolate.ParentAttribute] Book parent, CancellationToken ct)
        => Task.FromResult(new Author());
}

[HotChocolate.ExtendObjectType<Book>]
public static class BookExtensions2
{
    [HotChocolate.GraphQLIgnore]
    public static Task<Author> GetAuthorAsync([HotChocolate.ParentAttribute] Book parent, CancellationToken ct)
        => Task.FromResult(new Author());
}
";

        await Verifier.VerifyAnalyzerAsync(testCode);
    }
}
