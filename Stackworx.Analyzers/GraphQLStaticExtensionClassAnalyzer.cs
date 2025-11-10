namespace Stackworx.Analyzers;

using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public class GraphQLStaticExtensionClassAnalyzer : DiagnosticAnalyzer
    {
        private const string DiagnosticId = "SWGQL03";
        private const string Title = "Class with [ExtendObjectType] must be static";
        private const string MessageFormat = "Class '{0}' is annotated with [ExtendObjectType] and must be static";
        private const string Description = "Classes annotated with [ExtendObjectType] should be static.";
        private const string Category = "GraphQL";

        public static readonly DiagnosticDescriptor Rule = new(
            DiagnosticId,
            Title,
            MessageFormat,
            Category,
            DiagnosticSeverity.Error,
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
            if (context.Symbol is not INamedTypeSymbol classSymbol)
            {
                return;
            }

            // We only care about class types
            if (classSymbol.TypeKind != TypeKind.Class)
            {
                return;
            }

            foreach (var attribute in classSymbol.GetAttributes())
            {
                var attrClass = attribute.AttributeClass;
                if (attrClass == null)
                    continue;

                if (attrClass.Name == "ExtendObjectTypeAttribute" ||
                    attrClass.ToDisplayString().EndsWith(".ExtendObjectTypeAttribute"))
                {
                    if (!classSymbol.IsStatic)
                    {
                        var diagnostic = Diagnostic.Create(
                            Rule,
                            classSymbol.Locations.FirstOrDefault(),
                            classSymbol.Name);
                        context.ReportDiagnostic(diagnostic);
                    }

                    break;
                }
            }
        }
    }