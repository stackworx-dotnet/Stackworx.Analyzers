namespace Stackworx.Analyzers;

using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class AvoidImplicitDateTimeToDateTimeOffsetAnalyzer : DiagnosticAnalyzer
{
    private const string DiagnosticId = "SW0001";

    private static readonly LocalizableString Title =
        "Avoid implicit DateTime → DateTimeOffset conversion";

    private static readonly LocalizableString MessageFormat =
        "Implicit conversion from 'System.DateTime' to 'System.DateTimeOffset' is banned; make the conversion explicit";

    private static readonly LocalizableString Description =
        "Implicitly converting DateTime to DateTimeOffset hides offset/Kind semantics. Use an explicit construction instead.";

    private const string Category = "Usage";

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
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

        context.RegisterOperationAction(AnalyzeConversion, OperationKind.Conversion);
    }

    private static void AnalyzeConversion(OperationAnalysisContext ctx)
    {
        var conv = (IConversionOperation)ctx.Operation;

        // Only flag implicit, user-code conversions (not compiler generated).
        if (!conv.IsImplicit)
            return;

        var from = conv.Operand?.Type;
        var to = conv.Type;
        if (from is null || to is null)
            return;

        // Resolve canonical symbols once per callback
        var dtSymbol = ctx.Compilation.GetSpecialType(SpecialType.System_DateTime);
        var dtoSymbol = ctx.Compilation.GetTypeByMetadataName("System.DateTimeOffset");
        if (dtoSymbol is null)
            return; // defensive: target framework without DTO

        if (IsTypeOrNullableOf(from, dtSymbol) && IsTypeOrNullableOf(to, dtoSymbol))
        {
            ctx.ReportDiagnostic(Diagnostic.Create(Rule, conv.Syntax.GetLocation()));
        }
    }

    private static bool IsTypeOrNullableOf(ITypeSymbol type, ITypeSymbol target)
    {
        // direct match
        if (SymbolEqualityComparer.Default.Equals(type, target))
            return true;

        // Nullable<T> match
        if (type is INamedTypeSymbol named &&
            named.ConstructedFrom.SpecialType == SpecialType.System_Nullable_T &&
            named.TypeArguments.Length == 1)
        {
            return SymbolEqualityComparer.Default.Equals(named.TypeArguments[0], target);
        }

        return false;
    }
}