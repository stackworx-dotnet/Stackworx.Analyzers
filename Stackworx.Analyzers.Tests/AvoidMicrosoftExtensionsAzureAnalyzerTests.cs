namespace Stackworx.Analyzers.Tests;

using System.Threading.Tasks;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using Xunit;
using Verifier =
    Microsoft.CodeAnalysis.CSharp.Testing.CSharpAnalyzerVerifier<
        Stackworx.Analyzers.AvoidMicrosoftExtensionsAzureAnalyzer,
        Microsoft.CodeAnalysis.Testing.DefaultVerifier>;

public class AvoidMicrosoftExtensionsAzureAnalyzerTests
{
    private const string FakeAzureNamespace =
        """
        namespace Microsoft.Extensions.Azure
        {
            public sealed class AzureMarker { }
        }
        """;

    private const string FakeServiceCollection =
        """
        namespace Microsoft.Extensions.DependencyInjection
        {
            public interface IServiceCollection { }
            public sealed class ServiceCollection : IServiceCollection { }
        }
        """;

    private const string FakeAzureExtensions =
        """
        namespace Microsoft.Extensions.Azure
        {
            using Microsoft.Extensions.DependencyInjection;

            public static class AzureClientFactoryBuilderExtensions
            {
                public static IServiceCollection AddAzureClients(this IServiceCollection services) => services;
            }
        }
        """;

    [Fact]
    public async Task Flags_UsingDirective_ForMicrosoftExtensionsAzure()
    {
        const string src =
            """
            using {|#0:Microsoft.Extensions.Azure|};

            class C { }
            """;

        var test = new CSharpAnalyzerTest<Stackworx.Analyzers.AvoidMicrosoftExtensionsAzureAnalyzer, Microsoft.CodeAnalysis.Testing.DefaultVerifier>
        {
            ReferenceAssemblies = ReferenceAssemblies.Net.Net80,
            TestCode = src,
        };
        test.TestState.Sources.Add(FakeAzureNamespace);
        test.ExpectedDiagnostics.Add(
            Verifier
                .Diagnostic(Stackworx.Analyzers.AvoidMicrosoftExtensionsAzureAnalyzer.Rule)
                .WithLocation(0));

        await test.RunAsync();
    }

    [Fact]
    public async Task Flags_FullyQualified_Invocation_InForbiddenNamespace()
    {
        const string src =
            """
            class C
            {
                void M()
                {
                    Microsoft.Extensions.DependencyInjection.IServiceCollection services = new Microsoft.Extensions.DependencyInjection.ServiceCollection();
                    {|#0:Microsoft.Extensions.Azure.AzureClientFactoryBuilderExtensions.AddAzureClients(services)|};
                }
            }
            """;

        var test = new CSharpAnalyzerTest<Stackworx.Analyzers.AvoidMicrosoftExtensionsAzureAnalyzer, Microsoft.CodeAnalysis.Testing.DefaultVerifier>
        {
            ReferenceAssemblies = ReferenceAssemblies.Net.Net80,
            TestCode = src,
        };
        test.TestState.Sources.Add(FakeServiceCollection);
        test.TestState.Sources.Add(FakeAzureExtensions);
        test.ExpectedDiagnostics.Add(
            Verifier
                .Diagnostic(Stackworx.Analyzers.AvoidMicrosoftExtensionsAzureAnalyzer.Rule)
                .WithLocation(0));

        await test.RunAsync();
    }

    [Fact]
    public async Task DoesNotFlag_NearMatch_Namespace()
    {
        const string src =
            """
            using Microsoft.Extensions.Azureish;

            class C
            {
                void M()
                {
                    var marker = new Microsoft.Extensions.Azureish.AzureMarker();
                }
            }

            namespace Microsoft.Extensions.Azureish
            {
                public sealed class AzureMarker { }
            }
            """;

        await Verifier.VerifyAnalyzerAsync(src);
    }
}
