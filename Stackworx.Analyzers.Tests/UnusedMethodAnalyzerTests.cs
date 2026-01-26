namespace Stackworx.Analyzers.Tests;

using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using Xunit;
using Verifier =
    Microsoft.CodeAnalysis.CSharp.Testing.CSharpAnalyzerVerifier<
        Stackworx.Analyzers.UnusedMethodAnalyzer,
        Microsoft.CodeAnalysis.Testing.DefaultVerifier>;

public class UnusedMethodAnalyzerTests
{
    private static CSharpAnalyzerTest<UnusedMethodAnalyzer, Microsoft.CodeAnalysis.Testing.DefaultVerifier> CreateTest(string source)
    {
        var test = new CSharpAnalyzerTest<UnusedMethodAnalyzer, Microsoft.CodeAnalysis.Testing.DefaultVerifier>
        {
            ReferenceAssemblies = ReferenceAssemblies.Net.Net80,
            TestCode = source,
        };

        // The analyzer is disabled by default, so tests must enable it explicitly.
        test.TestState.AnalyzerConfigFiles.Add(("/.editorconfig",
            """
            root = true

            [*.cs]
            dotnet_diagnostic.SW002.severity = warning
            """));

        return test;
    }

    [Fact]
    public async Task Reports_WhenMethodIsNeverReferenced()
    {
        const string source =
            """
            public class C
            {
                void {|#0:Dead|}() { }
            }
            """;

        var test = CreateTest(source);
        test.ExpectedDiagnostics.Add(
            Verifier.Diagnostic(UnusedMethodAnalyzer.UnusedMethodRule)
                .WithLocation(0)
                .WithArguments("void C.Dead()")
                .WithSeverity(DiagnosticSeverity.Warning));

        await test.RunAsync();
    }

    [Fact]
    public async Task Reports_WhenMethodIsNotReferencedEvenIfItReferencesOthers()
    {
        const string source =
            """
            public class C
            {
                void Alive() { }

                void {|#0:Caller|}()
                {
                    Alive();
                }
            }
            """;

        var test = CreateTest(source);
        test.ExpectedDiagnostics.Add(
            Verifier.Diagnostic(UnusedMethodAnalyzer.UnusedMethodRule)
                .WithLocation(0)
                .WithArguments("void C.Caller()")
                .WithSeverity(DiagnosticSeverity.Warning));

        await test.RunAsync();
    }

    [Fact]
    public async Task Reports_WhenBaseVirtualMethodIsNeverReferenced()
    {
        const string source =
            """
            public class Base
            {
                public virtual void {|#0:M|}() { }
            }

            public class C : Base
            {
                public override void M() { }
            }
            """;

        var test = CreateTest(source);
        test.ExpectedDiagnostics.Add(
            Verifier.Diagnostic(UnusedMethodAnalyzer.UnusedMethodRule)
                .WithLocation(0)
                .WithArguments("void Base.M()")
                .WithSeverity(DiagnosticSeverity.Warning));

        await test.RunAsync();
    }

    [Fact]
    public async Task Reports_WhenExplicitInterfaceImplementationIsNeverReferenced()
    {
        const string source =
            """
            public interface IFoo
            {
                void M();
            }

            public class C : IFoo
            {
                void IFoo.M() { }
            }
            """;

        var test = CreateTest(source);
        test.ExpectedDiagnostics.Add(
            Verifier.Diagnostic(UnusedMethodAnalyzer.UnusedMethodRule)
                // The analyzer currently reports the interface method symbol location (the 'M' in IFoo),
                // not the explicit implementation identifier.
                .WithSpan(3, 10, 3, 11)
                .WithArguments("void IFoo.M()")
                .WithSeverity(DiagnosticSeverity.Warning));

        await test.RunAsync();
    }

    [Fact]
    public async Task DoesNotReport_WhenClassImplementsIDisposableAndDisposeIsNeverReferenced()
    {
        const string source =
            """
            using System;

            public class C : IDisposable
            {
                public void Dispose() { }
            }
            """;

        var test = CreateTest(source);
        await test.RunAsync();
    }

    [Fact]
    public async Task DoesNotReport_WhenClassImplementsIAsyncDisposableAndDisposeAsyncIsNeverReferenced()
    {
        const string source =
            """
            using System;
            using System.Threading.Tasks;

            public class C : IAsyncDisposable
            {
                public ValueTask DisposeAsync() => default;
            }
            """;

        var test = CreateTest(source);
        await test.RunAsync();
    }
}
