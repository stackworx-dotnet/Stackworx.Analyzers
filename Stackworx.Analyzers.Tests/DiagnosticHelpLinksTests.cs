namespace Stackworx.Analyzers.Tests;

using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis.Diagnostics;
using Xunit;

public class DiagnosticHelpLinksTests
{
    [Fact]
    public void AllSupportedDiagnostics_HaveExpectedDocumentationLinks()
    {
        var expectedLinks = new Dictionary<string, string>
        {
            ["SW001"] = "https://stackworx-dotnet.github.io/Stackworx.Analyzers/docs/rules/sw001-avoid-implicit-datetime-to-datetimeoffset",
            ["SW002"] = "https://stackworx-dotnet.github.io/Stackworx.Analyzers/docs/rules/sw002-unused-method",
            ["SW101"] = "https://stackworx-dotnet.github.io/Stackworx.Analyzers/docs/rules/sw101-forbidden-namespace-reference",
            ["SW102"] = "https://stackworx-dotnet.github.io/Stackworx.Analyzers/docs/rules/sw102-forbidden-namespace-using",
            ["SW103"] = "https://stackworx-dotnet.github.io/Stackworx.Analyzers/docs/rules/sw103-avoid-microsoft-extensions-azure",
            ["SWGQL01"] = "https://stackworx-dotnet.github.io/Stackworx.Analyzers/docs/rules/swgql01-static-extension-method-validation",
            ["SWGQL02"] = "https://stackworx-dotnet.github.io/Stackworx.Analyzers/docs/rules/swgql02-unused-dataloader-interface",
            ["SWGQL03"] = "https://stackworx-dotnet.github.io/Stackworx.Analyzers/docs/rules/swgql03-graphql-extension-class-static",
            ["SWGQL04"] = "https://stackworx-dotnet.github.io/Stackworx.Analyzers/docs/rules/swgql04-graphql-duplicate-extension-field",
            ["SWGQL05"] = "https://stackworx-dotnet.github.io/Stackworx.Analyzers/docs/rules/swgql05-06-hotchocolate-types-usedimplicitly",
            ["SWGQL06"] = "https://stackworx-dotnet.github.io/Stackworx.Analyzers/docs/rules/swgql05-06-hotchocolate-types-usedimplicitly",
        };

        DiagnosticAnalyzer[] analyzers =
        [
            new AvoidImplicitDateTimeToDateTimeOffsetAnalyzer(),
            new AvoidMicrosoftExtensionsAzureAnalyzer(),
            new GraphQLDuplicateFieldAnalyzer(),
            new GraphQLHotChocolateTypeUsedImplicitlyAnalyzer(),
            new GraphQLStaticExtensionClassAnalyzer(),
            new GraphQLStaticModifierExtensionMethodAnalyzer(),
            new GraphQLUnusedDataLoaderAnalyzer(),
            new NamespaceInternalAnalyzer(),
            new UnusedMethodAnalyzer(),
        ];

        var descriptors = analyzers
            .SelectMany(static analyzer => analyzer.SupportedDiagnostics)
            .ToArray();

        Assert.Equal(expectedLinks.Count, descriptors.Length);

        foreach (var descriptor in descriptors)
        {
            Assert.True(expectedLinks.TryGetValue(descriptor.Id, out var expectedLink), $"Unexpected diagnostic id: {descriptor.Id}");
            Assert.Equal(expectedLink, descriptor.HelpLinkUri);
        }
    }
}
