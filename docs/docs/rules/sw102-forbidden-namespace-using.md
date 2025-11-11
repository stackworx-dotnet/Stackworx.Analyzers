---
sidebar_position: 7
---

# SW102: Forbidden namespace using

## Overview

**Rule ID:** SW102  
**Category:** Architecture  
**Severity:** Warning  
**Status:** Currently disabled

## Description

This rule prevents `using` directives that import feature-internal namespaces from outside the feature. It works in conjunction with SW101 to enforce architectural boundaries, specifically catching explicit `using` statements that target internal namespaces.

## When This Rule Triggers

The analyzer reports a diagnostic when:
- A `using` directive imports a feature's internal namespace
- The code using the directive is outside that feature

## Why This Matters

### Explicit Import Prevention

While SW101 flags direct type references, this rule catches the more explicit case of importing entire namespaces:
- Prevents accidental pollution of the namespace
- Makes violations more obvious during code review
- Catches problems at the import stage before actual usage
- Serves as an early warning system

## How to Fix

### Option 1: Remove the using directive

Delete the `using` statement that imports the internal namespace:

```csharp
// ❌ WRONG
using Users.Internal;

public class OrderService
{
    // ...
}

// ✅ CORRECT
public class OrderService
{
    // ...
}
```

### Option 2: Use only public APIs

Reference types through their public namespaces:

```csharp
// ❌ WRONG
using Users.Internal;
using Users.Internal.Helpers;

// ✅ CORRECT
using Users;

public class OrderProcessor
{
    private readonly IUserService userService;
}
```

### Option 3: Move within the feature

If the code needs access to internal implementations, move it into the feature:

```csharp
// ❌ WRONG - Outside Users feature
// File: Orders/UserValidator.cs
using Users.Internal;

public class UserValidator { }

// ✅ CORRECT - Inside Users feature
// File: Users/UserValidator.cs
namespace Users;

public class UserValidator { }
```

## Example

```csharp
// File: Users/Internal/UserCache.cs
namespace Users.Internal;

internal class UserCache
{
    public User? Get(int id) { /* ... */ }
}

// File: Orders/OrderService.cs
namespace Orders;

// ⚠️ SW102 - Using statement targets internal namespace
using Users.Internal;  // ❌ Violation

public class OrderService
{
    private UserCache cache = new();
}

// ✅ CORRECT - Use public interface
using Users;

public class OrderService
{
    private readonly IUserService userService;
}
```

## Difference from SW101

| Rule | Detects | Catches |
|------|---------|---------|
| **SW101** | Direct type references to internal types | `var user = new Users.Internal.UserHelper()` |
| **SW102** | `using` directives importing internal namespaces | `using Users.Internal;` |

Both rules work together to prevent unauthorized access to feature internals.

## Configuration

This rule is currently disabled to avoid false positives. To enable it:

```ini
[*.cs]
dotnet_diagnostic.SW102.severity = warning
```

To treat violations as errors:

```ini
[*.cs]
dotnet_diagnostic.SW102.severity = error
```

## Related Rules

- [SW101 - Forbidden reference to feature-internal namespace](./sw101-forbidden-namespace-reference)

## See Also

- SW101 documentation
- Clean Architecture principles
- Feature-driven development patterns
