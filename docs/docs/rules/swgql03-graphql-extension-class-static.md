---
sidebar_position: 5
---

# SWGQL03: GraphQL extension class static

## Overview

**Rule ID:** SWGQL03  
**Category:** GraphQL  
**Severity:** Error  
**Status:** Enabled by default

## Description

This rule enforces that classes annotated with `[ExtendObjectType]` must be declared as `static`. The `[ExtendObjectType]` attribute is used to extend GraphQL types defined in HotChocolate, and these extension classes should be static to properly support the GraphQL type system.

## When This Rule Triggers

The analyzer reports a diagnostic when:
- A class is decorated with the `[ExtendObjectType]` attribute
- The class is not declared as `static`

## Why This Matters

### HotChocolate Type Extension Pattern

In HotChocolate, when extending an existing GraphQL object type:
- The class must be **static** to represent a pure type extension
- Instance state in extension classes can lead to unexpected behavior
- Non-static extension classes violate HotChocolate's type system conventions

This rule ensures your GraphQL schema extensions follow HotChocolate best practices.

## How to Fix

Add the `static` modifier to the class declaration:

```csharp
// ❌ WRONG - non-static extension class
[ExtendObjectType(typeof(User))]
public class UserExtensions
{
    [GraphQLName("fullName")]
    public string GetFullName([Parent] User user)
    {
        return $"{user.FirstName} {user.LastName}";
    }
}

// ✅ CORRECT - static extension class
[ExtendObjectType(typeof(User))]
public static class UserExtensions
{
    [GraphQLName("fullName")]
    public static string GetFullName([Parent] User user)
    {
        return $"{user.FirstName} {user.LastName}";
    }
}
```

## Example

```csharp
using HotChocolate.Types;

// ⚠️ SWGQL03 - Non-static extension class
[ExtendObjectType(typeof(Post))]
public class PostExtensions
{
    public string GetSummary([Parent] Post post)
    {
        return post.Content.Length > 100 
            ? post.Content.Substring(0, 100) + "..." 
            : post.Content;
    }
}

// ✅ CORRECT - Static extension class
[ExtendObjectType(typeof(Post))]
public static class PostExtensions
{
    public static string GetSummary([Parent] Post post)
    {
        return post.Content.Length > 100 
            ? post.Content.Substring(0, 100) + "..." 
            : post.Content;
    }
}
```

## Configuration

This rule is enabled by default as an error. To change its severity:

```ini
[*.cs]
dotnet_diagnostic.SWGQL03.severity = warning
```

To disable it:

```ini
[*.cs]
dotnet_diagnostic.SWGQL03.severity = none
```

## Related Rules

- [SWGQL01 - Field extension methods on non-static classes must be instance methods](./swgql01-static-extension-method-validation)

## See Also

- [HotChocolate Type Extensions](https://chillicream.com/docs/hotchocolate/types#extend-types)
- [ExtendObjectType Attribute](https://chillicream.com/docs/hotchocolate/types#extending-types)
