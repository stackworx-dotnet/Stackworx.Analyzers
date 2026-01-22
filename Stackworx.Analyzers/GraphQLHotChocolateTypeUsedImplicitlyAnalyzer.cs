namespace Stackworx.Analyzers;

using System;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

/// <summary>
/// Ensures HotChocolate GraphQL type classes are annotated with [JetBrains.Annotations.UsedImplicitly].
/// Also warns when [UsedImplicitly] is present but the type does not look like a HotChocolate GraphQL type.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class GraphQLHotChocolateTypeUsedImplicitlyAnalyzer : DiagnosticAnalyzer
{
    public static readonly DiagnosticDescriptor MissingUsedImplicitlyRule = new(
        id: "SWGQL05",
        title: "HotChocolate GraphQL type should be marked as [UsedImplicitly]",
        messageFormat: "Type '{0}' looks like a HotChocolate GraphQL type but is missing [JetBrains.Annotations.UsedImplicitly]",
        category: "GraphQL.Usage",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description:
        "HotChocolate GraphQL types are often discovered via reflection. Mark them with [JetBrains.Annotations.UsedImplicitly] so IDE analyzers don't flag them as unused.");

    public static readonly DiagnosticDescriptor UsedImplicitlyOnNonGraphQLTypeRule = new(
        id: "SWGQL06",
        title: "[UsedImplicitly] applied to non-GraphQL type",
        messageFormat: "Type '{0}' has [JetBrains.Annotations.UsedImplicitly] but does not look like a HotChocolate GraphQL type",
        category: "GraphQL.Usage",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description:
        "Prefer limiting [UsedImplicitly] to types that are actually discovered implicitly (e.g., HotChocolate GraphQL types).",
        customTags: [WellKnownDiagnosticTags.Unnecessary]);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        ImmutableArray.Create(MissingUsedImplicitlyRule, UsedImplicitlyOnNonGraphQLTypeRule);

    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.RegisterSymbolAction(AnalyzeNamedType, SymbolKind.NamedType);
    }

    private static void AnalyzeNamedType(SymbolAnalysisContext context)
    {
        if (context.Symbol is not INamedTypeSymbol typeSymbol)
        {
            return;
        }

        // Only source declarations
        if (typeSymbol.DeclaringSyntaxReferences.Length == 0)
        {
            return;
        }

        if (typeSymbol.TypeKind is not TypeKind.Class)
        {
            return;
        }

        // Skip nested/private types (usually not DI/GraphQL entry points)
        if (typeSymbol.DeclaredAccessibility is not (Accessibility.Public or Accessibility.Internal))
        {
            return;
        }

        var hasUsedImplicitly = HasUsedImplicitly(typeSymbol);
        var isGraphQlType = IsHotChocolateGraphQlType(typeSymbol);

        if (isGraphQlType && !hasUsedImplicitly)
        {
            context.ReportDiagnostic(
                Diagnostic.Create(
                    MissingUsedImplicitlyRule,
                    typeSymbol.Locations.FirstOrDefault(),
                    typeSymbol.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat)));
        }
        else if (!isGraphQlType && hasUsedImplicitly)
        {
            context.ReportDiagnostic(
                Diagnostic.Create(
                    UsedImplicitlyOnNonGraphQLTypeRule,
                    typeSymbol.Locations.FirstOrDefault(),
                    typeSymbol.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat)));
        }
    }

    private static bool HasUsedImplicitly(INamedTypeSymbol typeSymbol)
    {
        foreach (var attribute in typeSymbol.GetAttributes())
        {
            var attrClass = attribute.AttributeClass;
            if (attrClass is null)
            {
                continue;
            }

            if (attrClass.Name is "UsedImplicitlyAttribute" or "UsedImplicitly")
            {
                return true;
            }

            var full = attrClass.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
            if (full.EndsWith(".UsedImplicitlyAttribute", StringComparison.Ordinal) ||
                full.Equals("global::JetBrains.Annotations.UsedImplicitlyAttribute", StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsHotChocolateGraphQlType(INamedTypeSymbol typeSymbol)
    {
        // Attributes on the type: [QueryType], [MutationType], [SubscriptionType], [ObjectType], [ExtendObjectType]
        // In HotChocolate these live in HotChocolate.Types, but tests often stub them elsewhere.
        foreach (var attr in typeSymbol.GetAttributes())
        {
            var attrClass = attr.AttributeClass;
            if (attrClass is null)
            {
                continue;
            }

            var name = attrClass.Name;
            if (IsHotChocolateTypeAttributeName(name))
            {
                return true;
            }

            var full = attrClass.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
            if (full.StartsWith("global::HotChocolate.Types.", StringComparison.Ordinal) &&
                (full.EndsWith("QueryTypeAttribute", StringComparison.Ordinal)
                 || full.EndsWith("MutationTypeAttribute", StringComparison.Ordinal)
                 || full.EndsWith("SubscriptionTypeAttribute", StringComparison.Ordinal)
                 || full.EndsWith("ObjectTypeAttribute", StringComparison.Ordinal)
                 || full.EndsWith("ExtendObjectTypeAttribute", StringComparison.Ordinal)
                 || full.EndsWith("ExtendObjectType" , StringComparison.Ordinal)))
            {
                return true;
            }
        }

        // Base classes: QueryType, MutationType, SubscriptionType, ObjectType, ExtendObjectType
        // These are the ones you listed; adding SubscriptionType as a common sibling.
        foreach (var baseType in EnumerateBaseTypes(typeSymbol))
        {
            var btName = baseType.Name;
            if (IsHotChocolateTypeBaseName(btName))
            {
                return true;
            }

            var full = baseType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
            if (full is "global::HotChocolate.Types.QueryType" or
                "global::HotChocolate.Types.MutationType" or
                "global::HotChocolate.Types.SubscriptionType" or
                "global::HotChocolate.Types.ObjectType" or
                "global::HotChocolate.Types.ExtendObjectType")
            {
                return true;
            }
        }

        return false;

        static bool IsHotChocolateTypeAttributeName(string name) =>
            name is "QueryTypeAttribute" or "QueryType" or
                "MutationTypeAttribute" or "MutationType" or
                "SubscriptionTypeAttribute" or "SubscriptionType" or
                "ObjectTypeAttribute" or "ObjectType" or
                "ExtendObjectTypeAttribute" or "ExtendObjectType";

        static bool IsHotChocolateTypeBaseName(string name) =>
            name is "QueryType" or "MutationType" or "SubscriptionType" or "ObjectType" or "ExtendObjectType";

        static ImmutableArray<INamedTypeSymbol> EnumerateBaseTypes(INamedTypeSymbol symbol)
        {
            var builder = ImmutableArray.CreateBuilder<INamedTypeSymbol>();
            var current = symbol.BaseType;
            while (current is not null)
            {
                builder.Add(current);
                current = current.BaseType;
            }
            return builder.ToImmutable();
        }
    }
}

