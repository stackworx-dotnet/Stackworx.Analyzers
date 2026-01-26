namespace Stackworx.Analyzers.Tests;

using System.Threading.Tasks;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using Xunit;

public class UnusedMethodAnalyzerJetBrainsIgnoreTests
{
    private static CSharpAnalyzerTest<UnusedMethodAnalyzer, Microsoft.CodeAnalysis.Testing.DefaultVerifier> CreateTest(string source)
    {
        var test = new CSharpAnalyzerTest<UnusedMethodAnalyzer, Microsoft.CodeAnalysis.Testing.DefaultVerifier>
        {
            ReferenceAssemblies = ReferenceAssemblies.Net.Net80,
            TestCode = source,
        };

        test.TestState.AnalyzerConfigFiles.Add(("/.editorconfig",
            """
            root = true

            [*.cs]
            dotnet_diagnostic.SW002.severity = warning
            """));

        return test;
    }

    [Fact]
    public async Task DoesNotReport_WhenMethodHasJetBrainsPublicAPI()
    {
        const string source =
            """
            namespace JetBrains.Annotations
            {
                [System.AttributeUsage(System.AttributeTargets.All, AllowMultiple = true)]
                public sealed class PublicAPIAttribute : System.Attribute { }
            }

            public class C
            {
                [JetBrains.Annotations.PublicAPI]
                void ShouldBeIgnored() { }
            }
            """;

        var test = CreateTest(source);
        await test.RunAsync();
    }

    [Fact]
    public async Task DoesNotReport_WhenEnclosingTypeHasJetBrainsUsedImplicitly()
    {
        const string source =
            """
            namespace JetBrains.Annotations
            {
                [System.AttributeUsage(System.AttributeTargets.All, AllowMultiple = true)]
                public sealed class UsedImplicitlyAttribute : System.Attribute { }
            }

            [JetBrains.Annotations.UsedImplicitly]
            public class C
            {
                void ShouldBeIgnored() { }
            }
            """;

        var test = CreateTest(source);
        await test.RunAsync();
    }

    [Fact]
    public async Task DoesNotReport_WhenContainingOuterTypeHasJetBrainsUsedImplicitly()
    {
        const string source =
            """
            namespace JetBrains.Annotations
            {
                [System.AttributeUsage(System.AttributeTargets.All, AllowMultiple = true)]
                public sealed class UsedImplicitlyAttribute : System.Attribute { }
            }

            public class Outer
            {
                [JetBrains.Annotations.UsedImplicitly]
                public class Inner
                {
                    public class Nested
                    {
                        void ShouldBeIgnored() { }
                    }
                }
            }
            """;

        var test = CreateTest(source);
        await test.RunAsync();
    }
}

