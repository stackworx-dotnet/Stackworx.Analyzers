---
sidebar_position: 7
---

# SWGQL05 / SWGQL06: HotChocolate GraphQL types and `[UsedImplicitly]`

## Overview

**Rule IDs:** SWGQL05, SWGQL06  \
**Category:** GraphQL  \
**Severity:** Warning  \
**Status:** Enabled by default

## Description

HotChocolate discovers many schema types via reflection and conventions. That means a class can be *used at runtime* even if there are no direct references in your code.

These rules help keep your codebase consistent by requiring HotChocolate schema types to be annotated with `[JetBrains.Annotations.UsedImplicitly]`, and by flagging `[UsedImplicitly]` when it’s applied to types that don’t look like GraphQL types.

### SWGQL05 (Missing `[UsedImplicitly]`)
Triggers when a type looks like a HotChocolate GraphQL type but is missing `[JetBrains.Annotations.UsedImplicitly]`.

A type is considered a HotChocolate GraphQL type when it:

- Has HotChocolate schema attributes like `[QueryType]`, `[MutationType]`, `[SubscriptionType]`, `[ObjectType]`, `[ExtendObjectType]`, `[InputObjectType]`.
- Or derives from common schema base types like `ObjectType`, `InputObjectType`, `EnumType`, `FilterInputType` (including generic variants).

### SWGQL06 (`[UsedImplicitly]` on non-GraphQL types)
Triggers when `[UsedImplicitly]` is applied but the type does **not** look like a HotChocolate GraphQL type.

## Examples

### SWGQL05 example

```csharp
using HotChocolate.Types;

public class MyBookType : ObjectType
{
}
```

✅ Fix:

```csharp
using JetBrains.Annotations;
using HotChocolate.Types;

[UsedImplicitly]
public class MyBookType : ObjectType
{
}
```

### SWGQL06 example

```csharp
using JetBrains.Annotations;

[UsedImplicitly]
public class SomeHelper
{
}
```

✅ Fix: remove the attribute if it’s not needed.

## How to Fix

- For SWGQL05: add `[JetBrains.Annotations.UsedImplicitly]` to the type (and the `using JetBrains.Annotations;` if desired).
- For SWGQL06: remove `[UsedImplicitly]` or convert the type into a real schema type (if that was the intent).

