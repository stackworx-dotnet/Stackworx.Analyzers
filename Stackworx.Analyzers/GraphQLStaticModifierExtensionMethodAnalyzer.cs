namespace Stackworx.Analyzers;

using System;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class GraphQLStaticModifierExtensionMethodAnalyzer : DiagnosticAnalyzer
{
    // TODO: there is an alternative solution to make the extension class astic
    public static readonly DiagnosticDescriptor StaticMethodInNonStaticClassRule = new(
        id: "SWGQL01",
        title: "Field extension methods on non-static classes must be instance methods",
        messageFormat:
        "Field extension method '{0}' cannot be static because its containing class '{1}' is not static. Make it an instance method.",
        category: "GraphQL.Usage",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "GraphQL field extension methods must be instance methods when declared in non-static classes.");

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        ImmutableArray.Create(StaticMethodInNonStaticClassRule);

    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.RegisterSymbolAction(AnalyzeMethodForStaticInNonStaticClass, SymbolKind.Method);
    }

    private static void AnalyzeMethodForStaticInNonStaticClass(SymbolAnalysisContext context)
    {
        var method = (IMethodSymbol)context.Symbol;

        // Only analyze source ordinary methods
        if (method.DeclaringSyntaxReferences.Length == 0 ||
            method.MethodKind is not MethodKind.Ordinary)
        {
            return;
        }

        var containingType = method.ContainingType;
        if (containingType is null)
        {
            return;
        }

        // Rule: if the containing type is NOT static, then a field extension method must NOT be static.
        if (!containingType.IsStatic && method.IsStatic && HasParentParameter(method))
        {
            var location = method.Locations.FirstOrDefault();
            context.ReportDiagnostic(
                Diagnostic.Create(
                    StaticMethodInNonStaticClassRule,
                    location,
                    method.Name,
                    containingType.Name));
        }

        static bool HasParentParameter(IMethodSymbol m)
        {
            foreach (var p in m.Parameters)
            {
                if (HasParentAttribute(p))
                {
                    return true;
                }
            }
            return false;
        }

        static bool HasParentAttribute(IParameterSymbol parameter)
        {
            foreach (var attr in parameter.GetAttributes())
            {
                var attrClass = attr.AttributeClass;
                if (attrClass is null)
                    continue;

                // Simple name
                if (attrClass.Name is "ParentAttribute")
                    return true;

                // Fully-qualified checks
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