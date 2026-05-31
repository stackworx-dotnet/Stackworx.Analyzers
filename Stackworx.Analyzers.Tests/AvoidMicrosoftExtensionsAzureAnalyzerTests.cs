namespace Stackworx.Analyzers.Tests;

using System.Threading.Tasks;
using Xunit;
using Verifier =
    Microsoft.CodeAnalysis.CSharp.Testing.CSharpAnalyzerVerifier<
        Stackworx.Analyzers.AvoidMicrosoftExtensionsAzureAnalyzer,
        Microsoft.CodeAnalysis.Testing.DefaultVerifier>;

public class AvoidMicrosoftExtensionsAzureAnalyzerTests
{
    private const string FakeAzureNamespace =
        """
        namespace Microsoft.Extensions.Azure;
        public sealed class AzureMarker { }
        """;

    private const string FakeAzureExtensions =
        """
        namespace Microsoft.Extensions.DependencyInjection;
        public interface IServiceCollection { }
        public sealed class ServiceCollection : IServiceCollection { }

        namespace Microsoft.Extensions.Azure;
        using Microsoft.Extensions.DependencyInjection;

        public static class AzureClientFactoryBuilderExtensions
        {
            public static IServiceCollection AddAzureClients(this IServiceCollection services) => services;
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

        var expected = Verifier
            .Diagnostic(Stackworx.Analyzers.AvoidMicrosoftExtensionsAzureAnalyzer.Rule)
            .WithLocation(0);

        await Verifier.VerifyAnalyzerAsync([src, FakeAzureNamespace], expected);
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
                    Microsoft.Extensions.Azure.AzureClientFactoryBuilderExtensions.{|#0:AddAzureClients|}(services);
                }
            }
            """;

        var expected = Verifier
            .Diagnostic(Stackworx.Analyzers.AvoidMicrosoftExtensionsAzureAnalyzer.Rule)
            .WithLocation(0);

        await Verifier.VerifyAnalyzerAsync([src, FakeAzureExtensions], expected);
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

            namespace Microsoft.Extensions.Azureish;
            public sealed class AzureMarker { }
            """;

        await Verifier.VerifyAnalyzerAsync(src);
    }
}
