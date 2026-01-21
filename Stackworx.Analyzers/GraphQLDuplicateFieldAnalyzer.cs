namespace Stackworx.Analyzers;

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class GraphQLDuplicateFieldAnalyzer : DiagnosticAnalyzer
{
    public static readonly DiagnosticDescriptor DuplicateFieldRule = new(
        id: "SWGQL04",
        title: "Duplicate GraphQL extension field",
        messageFormat: "Duplicate extension field '{0}' for type '{1}'. Also defined at '{2}'.",
        category: "GraphQL.Usage",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "Detects duplicate extension fields for the same type across [ExtendObjectType<T>] extension classes.",
        customTags: [WellKnownDiagnosticTags.CompilationEnd]);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        ImmutableArray.Create(DuplicateFieldRule);

    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.Analyze | GeneratedCodeAnalysisFlags.ReportDiagnostics);
        context.RegisterCompilationStartAction(Start);
    }

    private sealed class State
    {
        public readonly ConcurrentDictionary<(ITypeSymbol Type, string FieldName), ConcurrentQueue<Location>> Seen =
            new(new KeyComparer());

        private sealed class KeyComparer : IEqualityComparer<(ITypeSymbol Type, string FieldName)>
        {
            public bool Equals((ITypeSymbol Type, string FieldName) x, (ITypeSymbol Type, string FieldName) y)
            {
                return SymbolEqualityComparer.Default.Equals(x.Type, y.Type) &&
                       StringComparer.OrdinalIgnoreCase.Equals(x.FieldName, y.FieldName);
            }

            public int GetHashCode((ITypeSymbol Type, string FieldName) obj)
            {
                unchecked
                {
                    var h1 = SymbolEqualityComparer.Default.GetHashCode(obj.Type);
                    var h2 = StringComparer.OrdinalIgnoreCase.GetHashCode(obj.FieldName);
                    return (h1 * 397) ^ h2;
                }
            }
        }
    }

    private static void Start(CompilationStartAnalysisContext context)
    {
        var state = new State();
        context.RegisterSymbolAction(c => AnalyzeNamedType(c, state), SymbolKind.NamedType);
        context.RegisterCompilationEndAction(c => ReportCompilationEnd(c, state));
    }

    private static void AnalyzeNamedType(SymbolAnalysisContext context, State state)
    {
        if (context.Symbol is not INamedTypeSymbol typeSymbol)
        {
            return;
        }

        if (typeSymbol.TypeKind != TypeKind.Class)
        {
            return;
        }

        if (typeSymbol.DeclaringSyntaxReferences.Length == 0)
        {
            return;
        }

        if (!TryGetExtendedType(typeSymbol, out var extendedType))
        {
            return;
        }

        // Only consider methods that can realistically become GraphQL fields.
        foreach (var method in typeSymbol.GetMembers().OfType<IMethodSymbol>())
        {
            if (method.MethodKind is not MethodKind.Ordinary)
            {
                continue;
            }

            // Ignore compiler-generated / implicit
            if (method.IsImplicitlyDeclared)
            {
                continue;
            }

            // Exclude inherited members like ToString/GetHashCode/etc.
            if (!SymbolEqualityComparer.Default.Equals(method.ContainingType, typeSymbol))
            {
                continue;
            }

            // Ignore fields that are explicitly hidden from the GraphQL schema.
            if (HasGraphQLIgnoreAttribute(method))
            {
                continue;
            }

            // HotChocolate field extension methods commonly are public or internal.
            if (method.DeclaredAccessibility is not (Accessibility.Public or Accessibility.Internal))
            {
                continue;
            }

            var normalized = NormalizeFieldName(method.Name);
            if (string.IsNullOrWhiteSpace(normalized))
            {
                continue;
            }

            var location = method.Locations.FirstOrDefault();
            if (location is null)
            {
                continue;
            }

            var key = (Type: extendedType, FieldName: normalized);
            state.Seen.GetOrAdd(key, _ => new ConcurrentQueue<Location>()).Enqueue(location);
        }
    }

    private static void ReportCompilationEnd(CompilationAnalysisContext context, State state)
    {
        foreach (var kvp in state.Seen)
        {
            var (extendedType, fieldName) = kvp.Key;
            var locations = kvp.Value.ToArray();

            if (locations.Length <= 1)
            {
                continue;
            }

            // Deterministic ordering so the "also defined at" path is stable.
            var ordered = locations
                .OrderBy(l => l.SourceTree?.FilePath ?? string.Empty, StringComparer.Ordinal)
                .ThenBy(l => l.SourceSpan.Start)
                .ToArray();

            var primary = ordered[0];
            var primaryPath = primary.GetLineSpan().Path;

            for (var i = 1; i < ordered.Length; i++)
            {
                var duplicate = ordered[i];

                // Avoid capturing the loop variable in the predicate (can confuse analyzers).
                var currentIndex = i;

                // Add all other occurrences as additional locations (including the first definition).
                var additionalLocations = ordered
                    .Where((_, idx) => idx != currentIndex)
                    .ToImmutableArray();

                context.ReportDiagnostic(
                    Diagnostic.Create(
                        DuplicateFieldRule,
                        duplicate,
                        additionalLocations,
                        ImmutableDictionary<string, string?>.Empty,
                        fieldName,
                        extendedType.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat),
                        primaryPath));
            }
        }
    }

    private static bool HasGraphQLIgnoreAttribute(ISymbol symbol)
    {
        // HotChocolate: [GraphQLIgnore]
        // StrawberryShake / other libs sometimes use: [GraphQLIgnore] / [GraphQLIgnoreAttribute]
        // We do both simple-name and fully-qualified checks because tests stub attributes.
        foreach (var attr in symbol.GetAttributes())
        {
            var attrClass = attr.AttributeClass;
            if (attrClass is null)
            {
                continue;
            }

            if (attrClass.Name is "GraphQLIgnore" or "GraphQLIgnoreAttribute")
            {
                return true;
            }

            var full = attrClass.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
            if (full.EndsWith(".GraphQLIgnoreAttribute", StringComparison.Ordinal) ||
                full.EndsWith(".GraphQLIgnore", StringComparison.Ordinal) ||
                full.Equals("global::HotChocolate.GraphQLIgnoreAttribute", StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private static bool TryGetExtendedType(INamedTypeSymbol classSymbol, out ITypeSymbol extendedType)
    {
        extendedType = null!;

        foreach (var attribute in classSymbol.GetAttributes())
        {
            var attrClass = attribute.AttributeClass;
            if (attrClass is null)
            {
                continue;
            }

            if (attrClass.Name != "ExtendObjectTypeAttribute" &&
                !attrClass.ToDisplayString().EndsWith(".ExtendObjectTypeAttribute", StringComparison.Ordinal))
            {
                continue;
            }

            // Generic form: ExtendObjectTypeAttribute<T>
            if (attribute.AttributeClass is { IsGenericType: true, TypeArguments.Length: 1 })
            {
                extendedType = attribute.AttributeClass.TypeArguments[0];
                return true;
            }

            // If it compiles as a non-generic attribute in tests, fall back to ctor arg.
            if (attribute.ConstructorArguments.Length == 1 &&
                attribute.ConstructorArguments[0].Kind == TypedConstantKind.Type &&
                attribute.ConstructorArguments[0].Value is ITypeSymbol t)
            {
                extendedType = t;
                return true;
            }
        }

        return false;
    }

    private static string NormalizeFieldName(string methodName)
    {
        if (string.IsNullOrEmpty(methodName))
        {
            return methodName;
        }

        var name = methodName;

        if (name.StartsWith("Get", StringComparison.Ordinal) && name.Length > 3)
        {
            name = name.Substring(3);
        }

        if (name.EndsWith("Async", StringComparison.Ordinal) && name.Length > 5)
        {
            name = name.Substring(0, name.Length - 5);
        }

        return name;
    }
}
