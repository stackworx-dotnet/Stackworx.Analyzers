---
sidebar_position: 6
---

# SWGQL04: Duplicate GraphQL extension field

## Overview

**Rule ID:** SWGQL04  
**Category:** GraphQL  
**Severity:** Error  
**Status:** Enabled by default

## Description

This rule detects duplicate GraphQL field resolver methods defined across multiple `[ExtendObjectType<T>]` extension classes for the same GraphQL type.

In HotChocolate, extension methods can be used to add fields to an existing GraphQL type. If two methods (possibly in different extension classes) map to the same GraphQL field name for the same extended type, the schema can become ambiguous and is likely to fail schema initialization.

The analyzer flags duplicates at compilation end and reports the duplicate location(s) as additional diagnostic locations.

## What Counts as a “Duplicate”

The analyzer considers two resolver methods duplicates when all of the following are true:

- Both methods are declared in classes annotated with `ExtendObjectType` (generic or constructor-argument form).
- Both methods extend the **same** CLR type `T`.
- Both methods normalize to the **same field name**.

### Field name normalization

To better match common HotChocolate naming patterns, field names are normalized as follows:

- A leading `Get` prefix is removed.
- A trailing `Async` suffix is removed.
- Comparison is **case-insensitive**.

Examples:

- `GetUser()` → `User`
- `UserAsync()` → `User`
- `GetUserAsync()` → `User`

## When This Rule Triggers

The analyzer reports a diagnostic when:

- Two or more eligible resolver methods (public/internal, ordinary methods) in `[ExtendObjectType<T>]` classes map to the same normalized field name for the same extended type.

## Why This Matters

Duplicate fields often happen during refactoring or when multiple teams add resolvers to the same GraphQL type:

- One field was moved to a new extension class, but the old resolver wasn’t removed.
- Two features accidentally define the same logical field.

Catching duplicates at compile time avoids schema build failures and confusing runtime behavior.

## How to Fix

- Remove or rename one of the duplicate resolver methods.
- Merge resolvers into a single extension class where appropriate.
- If you intended two different fields, rename one of the methods so the resulting GraphQL field names differ after normalization.

### Example

```csharp
using HotChocolate;
using HotChocolate.Types;

public class User
{
    public int Id { get; set; }
}

[ExtendObjectType(typeof(User))]
public static class UserExtensionsA
{
    public static int GetId([Parent] User user) => user.Id;
}

[ExtendObjectType(typeof(User))]
public static class UserExtensionsB
{
    // ❌ SWGQL04 - duplicates "Id" (Get prefix is ignored)
    public static int Id([Parent] User user) => user.Id;
}
```

## Configuration

This rule is enabled by default as an error. To change its severity:

```ini
[*.cs]
dotnet_diagnostic.SWGQL04.severity = warning
```

To disable it:

```ini
[*.cs]
dotnet_diagnostic.SWGQL04.severity = none
```

## Related Rules

- [SWGQL01 - Field extension methods on non-static classes must be instance methods](./swgql01-static-extension-method-validation)
- [SWGQL03 - Class with [ExtendObjectType] must be static](./swgql03-graphql-extension-class-static)

