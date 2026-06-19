namespace Stackworx.Analyzers.Tests;

using System.Threading.Tasks;
using Xunit;
using Verifier =
    Microsoft.CodeAnalysis.CSharp.Testing.CSharpAnalyzerVerifier<
        Stackworx.Analyzers.BlazorUnusedComponentAnalyzer,
        Microsoft.CodeAnalysis.Testing.DefaultVerifier>;

public class BlazorUnusedComponentAnalyzerTests
{
    // Minimal stubs mimicking the Blazor surface the analyzer keys off of. The real Razor compiler
    // generates classes deriving from ComponentBase and emits [Route] for @page directives, and
    // renders child components through RenderTreeBuilder.OpenComponent<T>().
    private const string BlazorStubs = @"
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using JetBrains.Annotations;

namespace JetBrains.Annotations
{
    [System.AttributeUsage(System.AttributeTargets.All)]
    public sealed class UsedImplicitlyAttribute : System.Attribute { }

    [System.AttributeUsage(System.AttributeTargets.All)]
    public sealed class PublicAPIAttribute : System.Attribute { }
}

namespace Microsoft.AspNetCore.Components
{
    public abstract class ComponentBase { }

    public abstract class LayoutComponentBase : ComponentBase { }

    [System.AttributeUsage(System.AttributeTargets.Class, AllowMultiple = true)]
    public sealed class RouteAttribute : System.Attribute
    {
        public RouteAttribute(string template) { }
    }

    [System.AttributeUsage(System.AttributeTargets.Class)]
    public sealed class LayoutAttribute : System.Attribute
    {
        public LayoutAttribute(System.Type layoutType) { }
    }
}

namespace Microsoft.AspNetCore.Components.Rendering
{
    public sealed class RenderTreeBuilder
    {
        public void OpenComponent<TComponent>(int sequence) { }
    }
}
";

    [Fact]
    public async Task Reports_WhenComponentIsNeverReferenced()
    {
        var testCode = BlazorStubs + @"
public class {|#0:Orphan|} : ComponentBase { }
";

        var expected = Verifier.Diagnostic(BlazorUnusedComponentAnalyzer.UnusedComponentRule)
            .WithArguments("Orphan")
            .WithLocation(0);

        await Verifier.VerifyAnalyzerAsync(testCode, expected);
    }

    [Fact]
    public async Task DoesNotReport_WhenComponentIsRoutable()
    {
        var testCode = BlazorStubs + @"
[Route(""/counter"")]
public class Counter : ComponentBase { }
";

        await Verifier.VerifyAnalyzerAsync(testCode);
    }

    [Fact]
    public async Task DoesNotReport_WhenComponentIsRenderedByAnother()
    {
        // Mimics the generated code for a parent component rendering `<Child />`.
        var testCode = BlazorStubs + @"
public class Child : ComponentBase { }

[Route(""/parent"")]
public class Parent : ComponentBase
{
    public void BuildRenderTree(RenderTreeBuilder builder)
    {
        builder.OpenComponent<Child>(0);
    }
}
";

        await Verifier.VerifyAnalyzerAsync(testCode);
    }

    [Fact]
    public async Task DoesNotReport_WhenComponentIsReferencedAsType()
    {
        var testCode = BlazorStubs + @"
public class Dialog : ComponentBase { }

public class Consumer
{
    private Dialog _dialog; // a plain type reference counts as usage
}
";

        await Verifier.VerifyAnalyzerAsync(testCode);
    }

    [Fact]
    public async Task DoesNotReport_WhenComponentIsUsedAsLayout()
    {
        // `@layout MainLayout` emits [Layout(typeof(MainLayout))] on the consuming component.
        var testCode = BlazorStubs + @"
public class MainLayout : ComponentBase { }

[Layout(typeof(MainLayout))]
[Route(""/home"")]
public class Home : ComponentBase { }
";

        await Verifier.VerifyAnalyzerAsync(testCode);
    }

    [Fact]
    public async Task DoesNotReport_AbstractBaseComponent()
    {
        var testCode = BlazorStubs + @"
public abstract class BaseComponent : ComponentBase { }
";

        await Verifier.VerifyAnalyzerAsync(testCode);
    }

    [Fact]
    public async Task DoesNotReport_BaseComponentInheritedByUsedComponent()
    {
        // A concrete base that is extended (and the derived one is rendered) is considered used.
        var testCode = BlazorStubs + @"
public class CardBase : ComponentBase { }

public class Card : CardBase { }

[Route(""/page"")]
public class Page : ComponentBase
{
    public void BuildRenderTree(RenderTreeBuilder builder)
    {
        builder.OpenComponent<Card>(0);
    }
}
";

        await Verifier.VerifyAnalyzerAsync(testCode);
    }

    [Fact]
    public async Task Reports_OnlyTheUnusedComponent_WhenMixed()
    {
        var testCode = BlazorStubs + @"
public class UsedWidget : ComponentBase { }

public class {|#0:UnusedWidget|} : ComponentBase { }

[Route(""/host"")]
public class Host : ComponentBase
{
    public void BuildRenderTree(RenderTreeBuilder builder)
    {
        builder.OpenComponent<UsedWidget>(0);
    }
}
";

        var expected = Verifier.Diagnostic(BlazorUnusedComponentAnalyzer.UnusedComponentRule)
            .WithArguments("UnusedWidget")
            .WithLocation(0);

        await Verifier.VerifyAnalyzerAsync(testCode, expected);
    }

    [Fact]
    public async Task DoesNotReport_WhenAnnotatedWithUsedImplicitly()
    {
        var testCode = BlazorStubs + @"
[UsedImplicitly]
public class DynamicallyRendered : ComponentBase { }
";

        await Verifier.VerifyAnalyzerAsync(testCode);
    }

    [Fact]
    public async Task DoesNotReport_WhenAnnotatedWithPublicApi()
    {
        var testCode = BlazorStubs + @"
[PublicAPI]
public class SharedComponent : ComponentBase { }
";

        await Verifier.VerifyAnalyzerAsync(testCode);
    }

    [Fact]
    public async Task DoesNotReport_RootComponentOptedOutViaCodeBehindPartial()
    {
        // The Blazor `Routes` / `App` root components are referenced from another assembly (the
        // server project), so they look unused in their own compilation. A .razor file cannot carry
        // attributes, so the only opt-out is a code-behind partial. The [UsedImplicitly] sits on a
        // different partial declaration than the ComponentBase-deriving (generated) one; Roslyn
        // aggregates attributes across all partials, so it is still honoured.
        var testCode = BlazorStubs + @"
public partial class Routes : ComponentBase { }

[UsedImplicitly]
public partial class Routes { }
";

        await Verifier.VerifyAnalyzerAsync(testCode);
    }

    [Fact]
    public async Task DoesNotReport_LayoutComponent()
    {
        // Layouts (deriving from LayoutComponentBase) are selected by name via @layout /
        // _Imports.razor / a router DefaultLayout, often from another assembly, so they are
        // excluded like routable @page components even with no direct reference.
        var testCode = BlazorStubs + @"
public class EmptyLayout : LayoutComponentBase { }
";

        await Verifier.VerifyAnalyzerAsync(testCode);
    }

    [Fact]
    public async Task DoesNotReport_GeneratedCodeComponent()
    {
        // Mimics a StrawberryShake-generated UseQuery<T> component: derives from ComponentBase
        // (transitively) and carries [GeneratedCode]. Not user-editable => must not be flagged.
        var testCode = BlazorStubs + @"
public abstract class UseQuery<TResult> : ComponentBase { }

[System.CodeDom.Compiler.GeneratedCode(""StrawberryShake"", ""15.1.5.0"")]
public partial class UseExtractRosterPatterns : UseQuery<object> { }
";

        await Verifier.VerifyAnalyzerAsync(testCode);
    }

    [Fact]
    public async Task DoesNotReport_WhenBlazorNotReferenced()
    {
        // No ComponentBase in the compilation => analyzer is a no-op.
        const string testCode = @"
public class Orphan { }
";

        await Verifier.VerifyAnalyzerAsync(testCode);
    }
}
