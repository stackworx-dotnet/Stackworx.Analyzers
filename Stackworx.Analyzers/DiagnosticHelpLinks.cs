namespace Stackworx.Analyzers;

using System;

internal static class DiagnosticHelpLinks
{
    private const string RulesBaseUrl = "https://stackworx-dotnet.github.io/Stackworx.Analyzers/docs/rules/";

    public static string For(string diagnosticId) => RulesBaseUrl + (diagnosticId switch
    {
        "SW001" =>
            "sw001-avoid-implicit-datetime-to-datetimeoffset",
        "SW002" =>
            "sw002-unused-method",
        "SW101" =>
            "sw101-forbidden-namespace-reference",
        "SW102" =>
            "sw102-forbidden-namespace-using",
        "SW103" =>
            "sw103-avoid-microsoft-extensions-azure",
        "SWGQL01" =>
            "swgql01-static-extension-method-validation",
        "SWGQL02" =>
            "swgql02-unused-dataloader-interface",
        "SWGQL03" =>
            "swgql03-graphql-extension-class-static",
        "SWGQL04" =>
            "swgql04-graphql-duplicate-extension-field",
        "SWGQL05" or "SWGQL06" =>
            "swgql05-06-hotchocolate-types-usedimplicitly",
        _ => throw new ArgumentOutOfRangeException(nameof(diagnosticId), diagnosticId, "No documentation link configured."),
    });
}
