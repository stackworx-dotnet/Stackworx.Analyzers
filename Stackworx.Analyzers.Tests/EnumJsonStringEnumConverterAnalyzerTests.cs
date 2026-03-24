namespace Stackworx.Analyzers.Tests;

using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Xunit;
using Verifier =
    Microsoft.CodeAnalysis.CSharp.Testing.CSharpAnalyzerVerifier<
        Stackworx.Analyzers.EnumJsonStringEnumConverterAnalyzer,
        Microsoft.CodeAnalysis.Testing.DefaultVerifier>;

public class EnumJsonStringEnumConverterAnalyzerTests
{
    [Fact]
    public async Task ReportsWarning_EnumWithoutJsonConverterAttribute()
    {
        const string testCode = @"
using System.Text.Json.Serialization;

public enum {|#0:Status|}
{
    Active,
    Inactive
}
";

        var expected = Verifier.Diagnostic(EnumJsonStringEnumConverterAnalyzer.Rule)
            .WithArguments("Status")
            .WithLocation(0)
            .WithSeverity(DiagnosticSeverity.Warning);

        await Verifier.VerifyAnalyzerAsync(testCode, expected);
    }

    [Fact]
    public async Task NoWarning_EnumWithJsonStringEnumConverterAttribute()
    {
        const string testCode = @"
using System.Text.Json.Serialization;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum Status
{
    Active,
    Inactive
}
";

        await Verifier.VerifyAnalyzerAsync(testCode);
    }

    [Fact]
    public async Task ReportsWarning_EnumWithDifferentJsonConverterAttribute()
    {
        const string testCode = @"
using System.Text.Json.Serialization;

public class CustomEnumConverter : JsonConverter<int>
{
    public override int Read(ref System.Text.Json.Utf8JsonReader reader, System.Type typeToConvert, System.Text.Json.JsonSerializerOptions options)
        => reader.GetInt32();

    public override void Write(System.Text.Json.Utf8JsonWriter writer, int value, System.Text.Json.JsonSerializerOptions options)
        => writer.WriteNumberValue(value);
}

[JsonConverter(typeof(CustomEnumConverter))]
public enum {|#0:Status|}
{
    Active,
    Inactive
}
";

        var expected = Verifier.Diagnostic(EnumJsonStringEnumConverterAnalyzer.Rule)
            .WithArguments("Status")
            .WithLocation(0)
            .WithSeverity(DiagnosticSeverity.Warning);

        await Verifier.VerifyAnalyzerAsync(testCode, expected);
    }

    [Fact]
    public async Task NoWarning_ClassIsNotAnEnum()
    {
        const string testCode = @"
public class MyClass
{
    public string Name { get; set; }
}
";

        await Verifier.VerifyAnalyzerAsync(testCode);
    }
}
