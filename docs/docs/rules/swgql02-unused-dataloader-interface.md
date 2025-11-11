---
sidebar_position: 4
---

# SWGQL02: Unused DataLoader interface

## Overview

**Rule ID:** SWGQL02  
**Category:** GraphQL  
**Severity:** Info  
**Status:** Enabled by default

## Description

This rule identifies DataLoader interfaces that implement `GreenDonut.IDataLoader<,>` but don't appear to be referenced anywhere in the current compilation. This helps detect potentially unused DataLoaders that may have been left behind during refactoring.

## When This Rule Triggers

The analyzer reports a diagnostic when:
- An interface implements `GreenDonut.IDataLoader<TKey, TValue>`
- The interface is not referenced (instantiated or used) anywhere in the compilation
- GreenDonut is available in the project dependencies

## Why This Matters

DataLoaders are a key performance optimization pattern in GraphQL to batch database queries and prevent N+1 query problems. Unused DataLoaders typically indicate:
- Code that was removed during refactoring
- Dead code that should be cleaned up
- A DataLoader that was registered but never used

Cleaning up unused DataLoaders reduces complexity and maintenance burden.

## How to Fix

### Option 1: Use the DataLoader

If you have a DataLoader that should be used, ensure it's:
- Registered in your dependency injection container
- Used in field resolvers

```csharp
// ✅ CORRECT - Used in a resolver
public class UserResolver
{
    public async Task<User?> GetUserAsync(
        [Parent] Post post,
        [DataLoader] UserDataLoader userDataLoader)
    {
        return await userDataLoader.LoadAsync(post.UserId);
    }
}
```

### Option 2: Remove the unused DataLoader

If the DataLoader is no longer needed, simply delete the interface:

```csharp
// ❌ Unused interface - remove it
public interface IUnusedDataLoader : IDataLoader<int, string>
{
}
```

## Example

```csharp
using GreenDonut;

// ⚠️ SWGQL02 - This interface appears unused in the compilation
public interface IUserDataLoader : IDataLoader<int, User>
{
}

// ✅ CORRECT - This interface is used
public interface IPostDataLoader : IDataLoader<int, Post>
{
}

public class PostResolver
{
    public async Task<User?> GetAuthorAsync(
        [Parent] Post post,
        [DataLoader] IPostDataLoader postDataLoader)
    {
        return await postDataLoader.LoadAsync(post.Id);
    }
}
```

## Configuration

This rule is informational by default. To treat it as a warning or error:

```ini
[*.cs]
dotnet_diagnostic.SWGQL02.severity = warning
```

To disable it:

```ini
[*.cs]
dotnet_diagnostic.SWGQL02.severity = none
```

## Related Rules

- None

## See Also

- [GreenDonut DataLoader Documentation](https://chillicream.com/docs/greendonut)
- [HotChocolate DataLoaders](https://chillicream.com/docs/hotchocolate/fetching-data)
