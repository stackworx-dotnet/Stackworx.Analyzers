namespace Stackworx.Analyzers.Tests;

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis.Diagnostics;
using Xunit;

public class DiagnosticHelpLinksTests
{
    private const string BaseUrl = "https://stackworx-dotnet.github.io/Stackworx.Analyzers/docs/rules/";

    private static readonly Dictionary<string, string> ExpectedLinks = new()
    {
        ["SW001"] = BaseUrl + "sw001-avoid-implicit-datetime-to-datetimeoffset",
        ["SW002"] = BaseUrl + "sw002-unused-method",
        ["SW101"] = BaseUrl + "sw101-forbidden-namespace-reference",
        ["SW102"] = BaseUrl + "sw102-forbidden-namespace-using",
        ["SW103"] = BaseUrl + "sw103-avoid-microsoft-extensions-azure",
        ["SWGQL01"] = BaseUrl + "swgql01-static-extension-method-validation",
        ["SWGQL02"] = BaseUrl + "swgql02-unused-dataloader-interface",
        ["SWGQL03"] = BaseUrl + "swgql03-graphql-extension-class-static",
        ["SWGQL04"] = BaseUrl + "swgql04-graphql-duplicate-extension-field",
        ["SWGQL05"] = BaseUrl + "swgql05-06-hotchocolate-types-usedimplicitly",
        ["SWGQL06"] = BaseUrl + "swgql05-06-hotchocolate-types-usedimplicitly",
        ["SWBLZ01"] = BaseUrl + "swblz01-blazor-unused-component",
    };

    /// <summary>
    /// Discovers every analyzer in the production assembly by reflection rather than a hand-maintained
    /// list, so a newly added analyzer that forgets its help-link wiring fails this test instead of
    /// silently shipping without a documentation link.
    /// </summary>
    private static DiagnosticAnalyzer[] AllAnalyzers()
    {
        return typeof(UnusedMethodAnalyzer).Assembly
            .GetTypes()
            .Where(t => !t.IsAbstract && typeof(DiagnosticAnalyzer).IsAssignableFrom(t))
            .Select(t => (DiagnosticAnalyzer)Activator.CreateInstance(t)!)
            .ToArray();
    }

    [Fact]
    public void AllSupportedDiagnostics_HaveExpectedDocumentationLinks()
    {
        var descriptors = AllAnalyzers()
            .SelectMany(static analyzer => analyzer.SupportedDiagnostics)
            .ToArray();

        foreach (var descriptor in descriptors)
        {
            Assert.True(
                ExpectedLinks.TryGetValue(descriptor.Id, out var expectedLink),
                $"Diagnostic id '{descriptor.Id}' has no expected documentation link registered in this test.");
            Assert.Equal(expectedLink, descriptor.HelpLinkUri);
        }

        // Every registered link must correspond to a real diagnostic (no stale entries).
        var actualIds = descriptors.Select(d => d.Id).ToHashSet(StringComparer.Ordinal);
        foreach (var id in ExpectedLinks.Keys)
        {
            Assert.True(actualIds.Contains(id), $"Expected link registered for '{id}' but no analyzer reports it.");
        }
    }
}
