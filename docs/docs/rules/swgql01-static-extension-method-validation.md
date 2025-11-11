---
sidebar_position: 3
---

# SWGQL01: Static extension method validation

## Overview

**Rule ID:** SWGQL01  
**Category:** GraphQL  
**Severity:** Error  
**Status:** Enabled by default

## Description

This rule enforces that field extension methods with the `[Parent]` parameter on non-static classes must be instance methods, not static methods. This is a GraphQL-specific rule for HotChocolate resolvers.

## When This Rule Triggers

The analyzer reports a diagnostic when:
- A method has a parameter decorated with `[Parent]` attribute
- The method is declared as `static`
- The containing class is not static

## Why This Matters

### GraphQL Field Resolution Context

In HotChocolate, field extension methods use the `[Parent]` attribute to indicate that the parameter represents the parent object being resolved. These methods must:
- Be **instance methods** if the class is not static (to have access to instance state)
- Be **static methods** only if the containing class itself is static

Violating this creates runtime errors or unexpected behavior during GraphQL query execution.

## How to Fix

### Option 1: Make the method an instance method (if class is not static)

```csharp
public class UserExtensions
{
    // ❌ WRONG - static method in non-static class with [Parent]
    public static string GetFullName([Parent] User user)
    {
        return $"{user.FirstName} {user.LastName}";
    }
}

// ✅ CORRECT - instance method
public class UserExtensions
{
    public string GetFullName([Parent] User user)
    {
        return $"{user.FirstName} {user.LastName}";
    }
}
```

### Option 2: Make the class static (if you want static methods)

```csharp
// ✅ CORRECT - static class with static method
public static class UserExtensions
{
    public static string GetFullName([Parent] User user)
    {
        return $"{user.FirstName} {user.LastName}";
    }
}
```

## Configuration

This rule is enabled by default as an error. To change its severity:

```ini
[*.cs]
dotnet_diagnostic.SWGQL01.severity = warning
```

To disable it:

```ini
[*.cs]
dotnet_diagnostic.SWGQL01.severity = none
```

## Related Rules

- [SWGQL03 - Class with [ExtendObjectType] must be static](./swgql03-graphql-extension-class-static)

## See Also

- [HotChocolate Field Resolvers](https://chillicream.com/docs/hotchocolate/resolvers)
- [Parent attribute documentation](https://chillicream.com/docs/hotchocolate/resolvers#parent)
