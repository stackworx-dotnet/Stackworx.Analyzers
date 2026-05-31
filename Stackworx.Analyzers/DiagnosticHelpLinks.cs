namespace Stackworx.Analyzers;

using System;

internal static class DiagnosticHelpLinks
{
    private const string RulesBaseUrl = "https://stackworx-dotnet.github.io/Stackworx.Analyzers/docs/rules/";

    public static string For(string diagnosticId) => RulesBaseUrl + diagnosticId switch
    {
        "https://stackworx-dotnet.github.io/Stackworx.Analyzers/docs/rules/SW001" =>
            "sw001-avoid-implicit-datetime-to-datetimeoffset",
        "https://stackworx-dotnet.github.io/Stackworx.Analyzers/docs/rules/SW002" =>
            "sw002-unused-method",
        "https://stackworx-dotnet.github.io/Stackworx.Analyzers/docs/rules/SW101" =>
            "sw101-forbidden-namespace-reference",
        "https://stackworx-dotnet.github.io/Stackworx.Analyzers/docs/rules/SW102" =>
            "sw102-forbidden-namespace-using",
        "https://stackworx-dotnet.github.io/Stackworx.Analyzers/docs/rules/SW103" =>
            "sw103-avoid-microsoft-extensions-azure",
        "https://stackworx-dotnet.github.io/Stackworx.Analyzers/docs/rules/SWGQL01" =>
            "swgql01-static-extension-method-validation",
        "https://stackworx-dotnet.github.io/Stackworx.Analyzers/docs/rules/SWGQL02" =>
            "swgql02-unused-dataloader-interface",
        "https://stackworx-dotnet.github.io/Stackworx.Analyzers/docs/rules/SWGQL03" =>
            "swgql03-graphql-extension-class-static",
        "https://stackworx-dotnet.github.io/Stackworx.Analyzers/docs/rules/SWGQL04" =>
            "swgql04-graphql-duplicate-extension-field",
        "https://stackworx-dotnet.github.io/Stackworx.Analyzers/docs/rules/SWGQL05" or
        "https://stackworx-dotnet.github.io/Stackworx.Analyzers/docs/rules/SWGQL06" =>
            "swgql05-06-hotchocolate-types-usedimplicitly",
        _ => throw new ArgumentOutOfRangeException(nameof(diagnosticId), diagnosticId, "No documentation link configured."),
    };
}
