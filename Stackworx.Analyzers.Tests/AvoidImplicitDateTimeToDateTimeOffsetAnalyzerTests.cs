namespace Stackworx.Analyzers.Tests;

using System.Threading.Tasks;
using Xunit;
using Verifier =
    Microsoft.CodeAnalysis.CSharp.Testing.CSharpAnalyzerVerifier<
        Stackworx.Analyzers.AvoidImplicitDateTimeToDateTimeOffsetAnalyzer,
        Microsoft.CodeAnalysis.Testing.DefaultVerifier>;

public class AvoidImplicitDateTimeToDateTimeOffsetAnalyzerTests
{
    [Fact]
    public async Task Flags_Implicit_Assignment()
    {
        const string src = @"
using System;

class C
{
    void M()
    {
        DateTime dt = DateTime.Now;
        DateTimeOffset dto = {|#0:dt|}; // implicit conversion
    }
}
";

        var expected = Verifier
            .Diagnostic(Stackworx.Analyzers.AvoidImplicitDateTimeToDateTimeOffsetAnalyzer.Rule)
            .WithLocation(0);

        await Verifier.VerifyAnalyzerAsync(src, expected);
    }

    [Fact]
    public async Task Flags_SourceNullable_Implicit_Assignment()
    {
        const string src = @"
using System;

class C
{
    void M()
    {
        DateTime? dt = DateTime.Now;
        DateTimeOffset dto = {|#0:dt!.Value|}; // implicit conversion
    }
}
";

        var expected = Verifier
            .Diagnostic(Stackworx.Analyzers.AvoidImplicitDateTimeToDateTimeOffsetAnalyzer.Rule)
            .WithLocation(0);

        await Verifier.VerifyAnalyzerAsync(src, expected);
    }

    [Fact]
    public async Task Flags_TargetNullable_Implicit_Assignment()
    {
        const string src = @"
using System;

class C
{
    void M()
    {
        DateTime dt = DateTime.Now;
        DateTimeOffset? dto = {|#0:dt|}; // implicit conversion
    }
}
";

        var expected = Verifier
            .Diagnostic(Stackworx.Analyzers.AvoidImplicitDateTimeToDateTimeOffsetAnalyzer.Rule)
            .WithLocation(0);

        await Verifier.VerifyAnalyzerAsync(src, expected);
    }

    [Fact]
    public async Task Flags_Implicit_Argument_Passing()
    {
        const string src = @"
using System;

class C
{
    void Log(DateTimeOffset value) { }

    void M()
    {
        DateTime dt = DateTime.UtcNow;
        Log({|#0:dt|}); // implicit conversion
    }
}
";

        var expected = Verifier
            .Diagnostic(Stackworx.Analyzers.AvoidImplicitDateTimeToDateTimeOffsetAnalyzer.Rule)
            .WithLocation(0);

        await Verifier.VerifyAnalyzerAsync(src, expected);
    }

    [Fact]
    public async Task DoesNotFlag_Explicit_New_Ctor()
    {
        const string src = @"
using System;

class C
{
    void M()
    {
        DateTime dt = DateTime.Now;
        DateTimeOffset dto = new DateTimeOffset(dt); // explicit: OK
    }
}
";
        await Verifier.VerifyAnalyzerAsync(src);
    }

    [Fact]
    public async Task DoesNotFlag_Explicit_Cast_Syntax()
    {
        const string src = @"
using System;

class C
{
    void M()
    {
        DateTime dt = DateTime.Now;
        DateTimeOffset dto = (DateTimeOffset)dt; // syntactically explicit: OK
    }
}
";
        await Verifier.VerifyAnalyzerAsync(src);
    }
}