using System.Threading.Tasks;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using Xunit;

namespace Stackworx.Analyzers.Tests;

/// <summary>
/// Tests for NamespaceInternalAnalyzer:
/// - NSINT002: using Contoso.Features.<Feature>.Internal from another feature is forbidden
/// - NSINT001: referencing a symbol in Contoso.Features.<Feature>.Internal from another feature is forbidden
/// - Same-feature access is allowed
/// - Test projects (IsTestProject=true) are exempt
/// </summary>
public class NamespaceInternalAnalyzerTests
{
    // EditorConfig enabling the analyzer for a single root "Contoso.Features"
    private const string EditorConfig =
        """
        root = true

        [*.cs]
        dotnet_code_quality.Stackworx.Analyzers.feature_namespaces = Contoso.Features
        dotnet_code_quality.NSINT.root_namespaces = Contoso.Features

        # Make violations errors for tests (you can change this as needed)
        dotnet_diagnostic.SW101.severity = error
        dotnet_diagnostic.SW102.severity = error
        """;

    // Same as above, but marks the project as a Test project => exemptions apply
    private const string EditorConfig_IsTestProject =
        """
        root = true

        [*.cs]
        dotnet_code_quality.Stackworx.Analyzers.feature_namespaces = Contoso.Features
        build_property.IsTestProject = true

        dotnet_diagnostic.SW101.severity = error
        dotnet_diagnostic.SW102.severity = error
        """;

    private const string FeatureAInternalSrc =
        """
        namespace Contoso.Features.FeatureA.Internal;
        public class Secret { }
        """;
    
    private const string FeatureASrc = 
        """
        namespace Contoso.Features.FeatureA
        public class Public {
            private Secret secret = new Secret();
        }               
        """;

    [Fact]
    public async Task Flags_Using_From_Other_Feature()
    {
        const string src =
            """
            namespace App.Client;
            using {|#0:Contoso.Features.FeatureA.Internal|};
            class C { }
            """;

        var test = new CSharpAnalyzerTest<Stackworx.Analyzers.NamespaceInternalAnalyzer, Microsoft.CodeAnalysis.Testing.DefaultVerifier>
        {
            ReferenceAssemblies = ReferenceAssemblies.Net.Net80,
            TestCode = src,
        };
        
        test.TestState.Sources.Add(FeatureAInternalSrc);
        test.TestState.AnalyzerConfigFiles.Add(("/.editorconfig", EditorConfig));

        test.ExpectedDiagnostics.Add(
            DiagnosticResult
                .CompilerWarning(NamespaceInternalAnalyzer.Id_Using)
                .WithLocation(0)); // Using to internal from outside the feature

        await test.RunAsync();
    }

    [Fact(Skip = "Disable Non Using Tests")]
    public async Task Flags_Reference_From_Other_Feature()
    {
        const string src =
            """
            namespace App.Client;
            class C
            {
                void M()
                {
                    var s = new {|#0:Contoso.Features.FeatureA.Internal.Secret|}();
                }
            }
            """;

        var test = new CSharpAnalyzerTest<Stackworx.Analyzers.NamespaceInternalAnalyzer, Microsoft.CodeAnalysis.Testing.DefaultVerifier>
        {
            ReferenceAssemblies = ReferenceAssemblies.Net.Net60
        };
        test.TestState.Sources.Add(src);
        test.TestState.Sources.Add(FeatureAInternalSrc);
        test.TestState.AnalyzerConfigFiles.Add(("/.editorconfig", EditorConfig));

        test.ExpectedDiagnostics.Add(
            DiagnosticResult
                .CompilerError(NamespaceInternalAnalyzer.Id_Reference)
                .WithLocation(0)); // Symbol reference to internal from outside the feature

        await test.RunAsync();
    }

    [Fact]
    public async Task Allows_Using_Inside_Same_Feature()
    {
        const string src =
            """
            namespace Contoso.Features.FeatureB
            {
                using Contoso.Features.FeatureB.Internal;
            
                class C
                {
                    void M()
                    {
                        var s = new Secret();
                    }
                }
            }

            namespace Contoso.Features.FeatureB.Internal
            {
                public class Secret { }
            }
            """;

        var test = new CSharpAnalyzerTest<Stackworx.Analyzers.NamespaceInternalAnalyzer, Microsoft.CodeAnalysis.Testing.DefaultVerifier>
        {
            ReferenceAssemblies = ReferenceAssemblies.Net.Net60
        };
        test.TestState.Sources.Add(src);
        test.TestState.AnalyzerConfigFiles.Add(("/.editorconfig", EditorConfig));

        // No diagnostics expected (same feature: Contoso.Features.FeatureB.*)
        await test.RunAsync();
    }

    [Fact(Skip = "Disable Non Using Tests")]
    public async Task Allows_Using_Inside_Same_Feature_NewNamespaces()
    {
        const string src =
            """
            namespace Contoso.Features.FeatureB;
            using Contoso.Features.FeatureB.Internal;

            class C
            {
                void M()
                {
                    var s = new Secret();
                }
            }

            namespace Contoso.Features.FeatureB.Internal;
            public class Secret { }
            """;

        var test = new CSharpAnalyzerTest<Stackworx.Analyzers.NamespaceInternalAnalyzer, Microsoft.CodeAnalysis.Testing.DefaultVerifier>
        {
            ReferenceAssemblies = ReferenceAssemblies.Net.Net60
        };
        test.TestState.Sources.Add(src);
        test.TestState.AnalyzerConfigFiles.Add(("/.editorconfig", EditorConfig));

        // No diagnostics expected (same feature: Contoso.Features.FeatureB.*)
        await test.RunAsync();
    }

    [Fact]
    public async Task TestProjects_Are_Exempt()
    {
        const string src =
            """
            namespace App.Client
            {
                using Contoso.Features.FeatureB.Internal;
            
                class C
                {
                    void M()
                    {
                        var s = new Contoso.Features.FeatureB.Internal.Secret();
                    }
                }
            }

            namespace Contoso.Features.FeatureB.Internal
            {
                public class Secret { }
            }
            """;

        var test = new CSharpAnalyzerTest<Stackworx.Analyzers.NamespaceInternalAnalyzer, Microsoft.CodeAnalysis.Testing.DefaultVerifier>
        {
            ReferenceAssemblies = ReferenceAssemblies.Net.Net60
        };
        test.TestState.Sources.Add(src);

        // Mark the project as a test project via analyzer config:
        test.TestState.AnalyzerConfigFiles.Add(("/.editorconfig", EditorConfig_IsTestProject));

        // No diagnostics expected (IsTestProject=true)
        await test.RunAsync();
    }

    [Fact(Skip = "Disable non Usings tests")]
    public async Task FullyQualified_Reference_From_Other_Feature_Is_Flagged()
    {
        const string src =
            """
            namespace App.Client;
            class C
            {
                void M()
                {
                    Contoso.Features.FeatureA.Internal.Secret s = new {|#0:Contoso.Features.FeatureA.Internal.Secret|}();
                }
            }
            """;

        var test = new CSharpAnalyzerTest<Stackworx.Analyzers.NamespaceInternalAnalyzer, Microsoft.CodeAnalysis.Testing.DefaultVerifier>
        {
            ReferenceAssemblies = ReferenceAssemblies.Net.Net60
        };

        test.TestState.Sources.Add(FeatureASrc);
        test.TestState.Sources.Add(FeatureAInternalSrc);
        test.TestState.Sources.Add(src);
        test.TestState.AnalyzerConfigFiles.Add(("/.editorconfig", EditorConfig));

        test.ExpectedDiagnostics.Add(
            DiagnosticResult
                .CompilerError(NamespaceInternalAnalyzer.Id_Reference)
                .WithLocation(0));

        await test.RunAsync();
    }
}