namespace Stackworx.Analyzers;

using System;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class GraphQLStaticExtensionMethodAnalyzer : DiagnosticAnalyzer
{
    // TODO: there is an alternative solution to make the extension class astic
    public static readonly DiagnosticDescriptor ParentOnStaticMethodRule = new(
        id: "SWGQL01",
        title: "Field extension methods cannot be static",
        messageFormat:
        "Field extension method '{0}' cannot be static. Make it an instance method.",
        category: "GraphQL.Usage",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: false,
        description: "GraphQL field extension methods should not be declared static.");

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        ImmutableArray.Create(ParentOnStaticMethodRule);

    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

        context.RegisterSymbolAction(AnalyzeMethodForParentOnStatic, SymbolKind.Method);
    }

    private static void AnalyzeMethodForParentOnStatic(SymbolAnalysisContext context)
    {
        var method = (IMethodSymbol)context.Symbol;

        // Only report for source methods
        if (method.DeclaringSyntaxReferences.Length == 0)
            return;

        if (!method.IsStatic)
        {
            return;
        }

        foreach (var p in method.Parameters)
        {
            if (HasParentAttribute(p))
            {
                // Fallback to parameter if location is missing
                var location = method.Locations.FirstOrDefault() ??
                               p.Locations.FirstOrDefault();
                
                context.ReportDiagnostic(
                    Diagnostic.Create(ParentOnStaticMethodRule, location, method.Name));
                
                break;
            }
        }

        static bool HasParentAttribute(IParameterSymbol parameter)
        {
            foreach (var attr in parameter.GetAttributes())
            {
                var attrClass = attr.AttributeClass;
                if (attrClass is null)
                {
                    continue;
                }

                // Match by simple name or fully qualified name to be resilient to using directives
                if (attrClass.Name is "ParentAttribute")
                {
                    return true;
                }

                var full = attrClass.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
                if (full.EndsWith(".ParentAttribute", StringComparison.Ordinal) ||
                    full.Equals("global::HotChocolate.ParentAttribute", StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }
    }
}