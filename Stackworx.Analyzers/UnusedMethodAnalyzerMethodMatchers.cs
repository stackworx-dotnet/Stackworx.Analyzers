namespace Stackworx.Analyzers;

using System;
using System.Linq;
using Microsoft.CodeAnalysis;

/// <summary>
/// Centralizes all method/symbol matching logic for <see cref="UnusedMethodAnalyzer"/>.
/// This keeps the analyzer itself focused on orchestration (collect, mark, report).
/// </summary>
internal static class UnusedMethodAnalyzerMethodMatchers
{
    public static bool IsIgnoredCandidate(IMethodSymbol method)
    {
        // Ignore Dispose pattern: Dispose / DisposeAsync are commonly called indirectly (using/await using/DI/framework/containers).
        if (IsDisposeMethod(method) || IsDisposeAsyncMethod(method) || IsHostedServiceLifecycleMethod(method))
        {
            return true;
        }

        // Ignore xUnit test entry points and lifecycle hooks.
        if (HasXunitTestMethodAttribute(method) || IsXunitAsyncLifetimeMethod(method))
        {
            return true;
        }

        // Ignore overrides: they might be invoked polymorphically.
        if (method.IsOverride)
        {
            return true;
        }

        // Ignore interface implementations: may be called through interface.
        if (method.ExplicitInterfaceImplementations.Length > 0)
        {
            return true;
        }

        // Also ignore implicit interface implementations (e.g., 'void IFoo.M()' implemented as 'public void M()').
        // Such methods can be invoked through an interface reference, so reporting them as unused is noisy.
        if (IsImplicitInterfaceImplementation(method))
        {
            return true;
        }

        // Ignore methods inside resolver types (HotChocolate GraphQL entry points).
        if (IsLikelyResolverContainingType(method.ContainingType))
        {
            return true;
        }

        // Ignore Azure Functions entry points.
        if (HasAzureFunctionsWorkerFunctionAttribute(method))
        {
            return true;
        }

        // Ignore HotChocolate DataLoader methods (used by source generators/runtime).
        if (HasHotChocolateDataLoaderAttribute(method))
        {
            return true;
        }

        // Ignore EF Core IEntityTypeConfiguration<T>.Configure implementations.
        if (IsEfCoreEntityTypeConfigurationConfigureMethod(method))
        {
            return true;
        }

        // Respect JetBrains annotations.
        if (HasJetBrainsPublicApiOrUsedImplicitly(method) || HasJetBrainsPublicApiOrUsedImplicitly(method.ContainingType))
        {
            return true;
        }

        // Ignore methods inside implicitly used containing types (walk containing types for nested types).
        for (var type = method.ContainingType?.ContainingType; type is not null; type = type.ContainingType)
        {
            if (HasJetBrainsPublicApiOrUsedImplicitly(type))
            {
                return true;
            }
        }

        // Ignore extension methods: their call-sites may exist in other projects/compilations and
        // usage can be hard to infer reliably. Avoid noisy false positives.
        if (method.IsExtensionMethod)
        {
            return true;
        }

        // Extra perf/noise filter: don't even track candidates in common framework/library namespaces.
        if (ShouldIgnoreCandidateNamespace(method))
        {
            return true;
        }

        return false;
    }

    public static bool IsIgnoredUsage(IMethodSymbol method) => ShouldIgnoreUsageNamespace(method);

    private static bool IsLikelyResolverContainingType(INamedTypeSymbol? type)
    {
        for (var current = type; current is not null; current = current.ContainingType)
        {
            if (HasResolverTypeAttribute(current))
            {
                return true;
            }
        }

        return false;
    }

    private static bool HasResolverTypeAttribute(INamedTypeSymbol type)
    {
        foreach (var attribute in type.GetAttributes())
        {
            var attrClass = attribute.AttributeClass;
            if (attrClass is null)
            {
                continue;
            }

            // Prefer matching by name suffix to avoid requiring HotChocolate references.
            // HotChocolate commonly marks resolver types with these attributes.
            var name = attrClass.Name;
            if (name is "QueryTypeAttribute" or "QueryType"
                or "MutationTypeAttribute" or "MutationType"
                or "SubscriptionTypeAttribute" or "SubscriptionType"
                or "ObjectTypeAttribute" or "ObjectType"
                or "ExtendObjectTypeAttribute" or "ExtendObjectType"
                or "InputObjectTypeAttribute" or "InputObjectType")
            {
                return true;
            }

            // Also handle fully qualified display strings.
            var full = attrClass.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
            if (full.Contains("HotChocolate", StringComparison.Ordinal)
                && (full.EndsWith("QueryTypeAttribute", StringComparison.Ordinal)
                    || full.EndsWith("MutationTypeAttribute", StringComparison.Ordinal)
                    || full.EndsWith("SubscriptionTypeAttribute", StringComparison.Ordinal)
                    || full.EndsWith("ObjectTypeAttribute", StringComparison.Ordinal)
                    || full.EndsWith("ExtendObjectTypeAttribute", StringComparison.Ordinal)
                    || full.EndsWith("InputObjectTypeAttribute", StringComparison.Ordinal)))
            {
                return true;
            }
        }

        return false;
    }

    private static bool ShouldIgnoreCandidateNamespace(IMethodSymbol method)
    {
        var ns = method.ContainingNamespace?.ToDisplayString() ?? string.Empty;
        if (string.IsNullOrEmpty(ns))
        {
            return false;
        }

        // Same filtering concept as ShouldIgnoreUsageNamespace, but for candidates.
        // In practice candidates are usually source-only anyway, but this also guards against
        // edge cases (e.g., linked files / unusual compilations).

        // Framework/BCL
        if (ns.Equals("System", StringComparison.Ordinal)
            || ns.StartsWith("System.", StringComparison.Ordinal))
        {
            return true;
        }

        // HotChocolate + friends
        if (ns.Equals("HotChocolate", StringComparison.Ordinal)
            || ns.StartsWith("HotChocolate.", StringComparison.Ordinal)
            || ns.Equals("GreenDonut", StringComparison.Ordinal)
            || ns.StartsWith("GreenDonut.", StringComparison.Ordinal))
        {
            return true;
        }

        // EF Core
        if (ns.Equals("Microsoft.EntityFrameworkCore", StringComparison.Ordinal)
            || ns.StartsWith("Microsoft.EntityFrameworkCore.", StringComparison.Ordinal))
        {
            return true;
        }

        return false;
    }

    private static bool ShouldIgnoreUsageNamespace(IMethodSymbol method)
    {
        var ns = method.ContainingNamespace?.ToDisplayString() ?? string.Empty;
        if (string.IsNullOrEmpty(ns))
        {
            return false;
        }

        // Framework/BCL
        if (ns.Equals("System", StringComparison.Ordinal)
            || ns.StartsWith("System.", StringComparison.Ordinal))
        {
            return true;
        }

        // HotChocolate + friends
        if (ns.Equals("HotChocolate", StringComparison.Ordinal)
            || ns.StartsWith("HotChocolate.", StringComparison.Ordinal)
            || ns.Equals("GreenDonut", StringComparison.Ordinal)
            || ns.StartsWith("GreenDonut.", StringComparison.Ordinal))
        {
            return true;
        }

        // EF Core
        if (ns.Equals("Microsoft.EntityFrameworkCore", StringComparison.Ordinal)
            || ns.StartsWith("Microsoft.EntityFrameworkCore.", StringComparison.Ordinal))
        {
            return true;
        }

        return false;
    }

    private static bool HasJetBrainsPublicApiOrUsedImplicitly(ISymbol? symbol)
    {
        if (symbol is null)
        {
            return false;
        }

        foreach (var attribute in symbol.GetAttributes())
        {
            var attrClass = attribute.AttributeClass;
            if (attrClass is null)
            {
                continue;
            }

            if (attrClass.Name is "PublicAPIAttribute" or "PublicAPI" or "UsedImplicitlyAttribute" or "UsedImplicitly")
            {
                return true;
            }

            var full = attrClass.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
            if (full.Equals("global::JetBrains.Annotations.PublicAPIAttribute", StringComparison.Ordinal)
                || full.Equals("global::JetBrains.Annotations.UsedImplicitlyAttribute", StringComparison.Ordinal)
                || full.EndsWith(".PublicAPIAttribute", StringComparison.Ordinal)
                || full.EndsWith(".UsedImplicitlyAttribute", StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsDisposeMethod(IMethodSymbol method)
    {
        if (method.Name != "Dispose" || method.Parameters.Length != 0)
        {
            return false;
        }

        // Treat explicit interface implementation and normal implementation the same.
        var containing = method.ContainingType;
        if (containing is null)
        {
            return false;
        }

        foreach (var iface in containing.AllInterfaces)
        {
            if (iface.SpecialType == SpecialType.System_IDisposable)
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsDisposeAsyncMethod(IMethodSymbol method)
    {
        if (method.Name != "DisposeAsync" || method.Parameters.Length != 0)
        {
            return false;
        }

        // IAsyncDisposable.DisposeAsync returns ValueTask.
        if (method.ReturnType is not INamedTypeSymbol named
            || named.ContainingNamespace.ToDisplayString() != "System.Threading.Tasks"
            || named.Name != "ValueTask")
        {
            return false;
        }

        var containing = method.ContainingType;
        if (containing is null)
        {
            return false;
        }

        // No SpecialType for IAsyncDisposable, so compare by metadata name.
        return containing.AllInterfaces.Any(i =>
            i.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat) == "global::System.IAsyncDisposable");
    }

    private static bool IsHostedServiceLifecycleMethod(IMethodSymbol method)
    {
        if (method.Parameters.Length != 1)
        {
            return false;
        }

        if (method.Name is not ("StartAsync" or "StopAsync"))
        {
            return false;
        }

        // StartAsync/StopAsync return Task.
        if (method.ReturnType is not INamedTypeSymbol returnType
            || returnType.ContainingNamespace.ToDisplayString() != "System.Threading.Tasks"
            || returnType.Name != "Task")
        {
            return false;
        }

        // Parameter is CancellationToken.
        var p0 = method.Parameters[0].Type;
        if (p0.ContainingNamespace.ToDisplayString() != "System.Threading"
            || p0.Name != "CancellationToken")
        {
            return false;
        }

        var containing = method.ContainingType;
        if (containing is null)
        {
            return false;
        }

        // Compare by metadata name to avoid needing the hosting ref in the analyzer project.
        return containing.AllInterfaces.Any(i =>
            i.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)
            == "global::Microsoft.Extensions.Hosting.IHostedService");
    }

    private static bool HasAzureFunctionsWorkerFunctionAttribute(ISymbol symbol)
    {
        foreach (var attribute in symbol.GetAttributes())
        {
            var attrClass = attribute.AttributeClass;
            if (attrClass is null)
            {
                continue;
            }

            // Attribute can be referenced as [Function] or [FunctionAttribute]
            if (attrClass.Name is "FunctionAttribute" or "Function")
            {
                var full = attrClass.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
                if (full.Equals("global::Microsoft.Azure.Functions.Worker.FunctionAttribute", StringComparison.Ordinal)
                    || full.EndsWith(".Microsoft.Azure.Functions.Worker.FunctionAttribute", StringComparison.Ordinal)
                    || full.EndsWith(".Functions.Worker.FunctionAttribute", StringComparison.Ordinal))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static bool HasHotChocolateDataLoaderAttribute(ISymbol symbol)
    {
        foreach (var attribute in symbol.GetAttributes())
        {
            var attrClass = attribute.AttributeClass;
            if (attrClass is null)
            {
                continue;
            }

            if (attrClass.Name is not ("DataLoaderAttribute" or "DataLoader"))
            {
                continue;
            }

            var full = attrClass.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
            if (full.Equals("global::HotChocolate.DataLoaderAttribute", StringComparison.Ordinal)
                || full.EndsWith(".HotChocolate.DataLoaderAttribute", StringComparison.Ordinal)
                || full.EndsWith(".DataLoaderAttribute", StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsEfCoreEntityTypeConfigurationConfigureMethod(IMethodSymbol method)
    {
        if (method.Name != "Configure" || method.Parameters.Length != 1)
        {
            return false;
        }

        // Must be an implementation of Microsoft.EntityFrameworkCore.IEntityTypeConfiguration<T>.Configure
        // so we avoid ignoring random Configure(...) methods.
        var p0 = method.Parameters[0].Type;
        if (p0 is not INamedTypeSymbol namedParam)
        {
            return false;
        }

        // Expect EntityTypeBuilder<TEntity>
        if (namedParam.Name != "EntityTypeBuilder" || namedParam.TypeArguments.Length != 1)
        {
            return false;
        }

        // Confirm containing namespace is Microsoft.EntityFrameworkCore.Metadata.Builders
        if (namedParam.ContainingNamespace.ToDisplayString() != "Microsoft.EntityFrameworkCore.Metadata.Builders")
        {
            return false;
        }

        var containing = method.ContainingType;
        if (containing is null)
        {
            return false;
        }

        // No SpecialType for IEntityTypeConfiguration<T>, so compare by metadata name.
        return containing.AllInterfaces.Any(i =>
            i.IsGenericType
            && i.Name == "IEntityTypeConfiguration"
            && i.TypeArguments.Length == 1
            && i.ContainingNamespace.ToDisplayString() == "Microsoft.EntityFrameworkCore"
            && i.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)
                .StartsWith("global::Microsoft.EntityFrameworkCore.IEntityTypeConfiguration<", StringComparison.Ordinal));
    }

    private static bool IsImplicitInterfaceImplementation(IMethodSymbol method)
    {
        var containing = method.ContainingType;
        if (containing is null)
        {
            return false;
        }

        // Ignore common non-user-facing methods already filtered by MethodKind above.
        if (method.MethodKind != MethodKind.Ordinary)
        {
            return false;
        }

        // Check whether any implemented interface member maps to this concrete method.
        // Using OriginalDefinition keeps generic interface methods consistent.
        foreach (var iface in containing.AllInterfaces)
        {
            foreach (var member in iface.GetMembers())
            {
                if (member is not IMethodSymbol interfaceMethod)
                {
                    continue;
                }

                // Skip explicit implementations (handled elsewhere).
                if (interfaceMethod.MethodKind != MethodKind.Ordinary)
                {
                    continue;
                }

                var impl = containing.FindImplementationForInterfaceMember(interfaceMethod);
                if (impl is IMethodSymbol implMethod
                    && SymbolEqualityComparer.Default.Equals(implMethod.OriginalDefinition, method.OriginalDefinition))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static bool HasXunitTestMethodAttribute(IMethodSymbol method)
    {
        foreach (var attribute in method.GetAttributes())
        {
            var attrClass = attribute.AttributeClass;
            if (attrClass is null)
            {
                continue;
            }

            // Typical: [Fact] / [Theory]
            if (attrClass.Name is "FactAttribute" or "Fact" or "TheoryAttribute" or "Theory")
            {
                var full = attrClass.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
                if (full.Equals("global::Xunit.FactAttribute", StringComparison.Ordinal)
                    || full.Equals("global::Xunit.TheoryAttribute", StringComparison.Ordinal)
                    || full.EndsWith(".Xunit.FactAttribute", StringComparison.Ordinal)
                    || full.EndsWith(".Xunit.TheoryAttribute", StringComparison.Ordinal))
                {
                    return true;
                }

                // If the attribute is named Fact/Theory but not fully qualified (test stubs), still treat it as xUnit.
                // This matches how our tests define minimal attribute types.
                if (attrClass.ContainingNamespace?.ToDisplayString() == "Xunit")
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static bool IsXunitAsyncLifetimeMethod(IMethodSymbol method)
    {
        if (method.Parameters.Length != 0)
        {
            return false;
        }

        if (method.Name is not ("InitializeAsync" or "DisposeAsync"))
        {
            return false;
        }

        // xUnit IAsyncLifetime methods return Task.
        if (method.ReturnType is not INamedTypeSymbol returnType
            || returnType.ContainingNamespace.ToDisplayString() != "System.Threading.Tasks"
            || returnType.Name != "Task")
        {
            return false;
        }

        var containing = method.ContainingType;
        if (containing is null)
        {
            return false;
        }

        // Compare by metadata name, no dependency on xUnit reference.
        return containing.AllInterfaces.Any(i =>
            i.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat) == "global::Xunit.IAsyncLifetime");
    }
}
