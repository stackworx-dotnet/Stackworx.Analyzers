namespace Stackworx.Analyzers;

using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class EnumJsonStringEnumConverterAnalyzer : DiagnosticAnalyzer
{
    private const string DiagnosticId = "SW003";
    private const string Title = "Enum must be annotated with [JsonConverter(typeof(JsonStringEnumConverter))]";
    private const string MessageFormat = "Enum '{0}' must be annotated with [JsonConverter(typeof(JsonStringEnumConverter))] to ensure string serialization";
    private const string Description = "Enums should be annotated with [JsonConverter(typeof(JsonStringEnumConverter))] to ensure they are serialized to their string value instead of their numeric value.";
    private const string Category = "Serialization";

    public static readonly DiagnosticDescriptor Rule = new(
        id: DiagnosticId,
        title: Title,
        messageFormat: MessageFormat,
        category: Category,
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: Description);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSymbolAction(AnalyzeSymbol, SymbolKind.NamedType);
    }

    private static void AnalyzeSymbol(SymbolAnalysisContext context)
    {
        if (context.Symbol is not INamedTypeSymbol typeSymbol)
        {
            return;
        }

        if (typeSymbol.TypeKind != TypeKind.Enum)
        {
            return;
        }

        var jsonConverterAttrType = context.Compilation.GetTypeByMetadataName(
            "System.Text.Json.Serialization.JsonConverterAttribute");
        var jsonStringEnumConverterType = context.Compilation.GetTypeByMetadataName(
            "System.Text.Json.Serialization.JsonStringEnumConverter");
        // Generic JsonStringEnumConverter<TEnum> introduced in .NET 8
        var jsonStringEnumConverterGenericType = context.Compilation.GetTypeByMetadataName(
            "System.Text.Json.Serialization.JsonStringEnumConverter`1");

        // If System.Text.Json is not referenced, skip analysis
        if (jsonConverterAttrType == null || (jsonStringEnumConverterType == null && jsonStringEnumConverterGenericType == null))
        {
            return;
        }

        if (!HasJsonStringEnumConverterAttribute(typeSymbol, jsonConverterAttrType, jsonStringEnumConverterType, jsonStringEnumConverterGenericType))
        {
            var diagnostic = Diagnostic.Create(
                Rule,
                typeSymbol.Locations.FirstOrDefault(),
                typeSymbol.Name);
            context.ReportDiagnostic(diagnostic);
        }
    }

    private static bool HasJsonStringEnumConverterAttribute(
        INamedTypeSymbol enumSymbol,
        INamedTypeSymbol jsonConverterAttrType,
        INamedTypeSymbol? jsonStringEnumConverterType,
        INamedTypeSymbol? jsonStringEnumConverterGenericType)
    {
        foreach (var attribute in enumSymbol.GetAttributes())
        {
            if (attribute.AttributeClass == null)
            {
                continue;
            }

            // Check if attribute is or inherits from JsonConverterAttribute
            var attrClass = attribute.AttributeClass;
            if (!InheritsFrom(attrClass, jsonConverterAttrType))
            {
                continue;
            }

            // Check if the constructor argument is typeof(JsonStringEnumConverter)
            // or typeof(JsonStringEnumConverter<TEnum>). JsonStringEnumConverter is sealed,
            // so we use original definition equality rather than an inheritance walk.
            if (attribute.ConstructorArguments.Length == 1 &&
                attribute.ConstructorArguments[0].Value is ITypeSymbol converterType)
            {
                var originalDef = converterType.OriginalDefinition;
                if ((jsonStringEnumConverterType != null &&
                     SymbolEqualityComparer.Default.Equals(originalDef, jsonStringEnumConverterType)) ||
                    (jsonStringEnumConverterGenericType != null &&
                     SymbolEqualityComparer.Default.Equals(originalDef, jsonStringEnumConverterGenericType)))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static bool InheritsFrom(ITypeSymbol type, ITypeSymbol baseType)
    {
        var current = type;
        while (current != null)
        {
            if (SymbolEqualityComparer.Default.Equals(current.OriginalDefinition, baseType.OriginalDefinition))
            {
                return true;
            }

            current = current.BaseType;
        }

        return false;
    }
}
