---
sidebar_position: 6
---

# SW101: Forbidden namespace reference

## Overview

**Rule ID:** SW101  
**Category:** Architecture  
**Severity:** Warning  
**Status:** Currently disabled

## Description

This rule prevents code from referencing types in feature-internal namespaces from outside the feature. Features can define internal namespaces (typically named with `.Internal` suffix) that should only be accessed from within the feature.

## When This Rule Triggers

The analyzer reports a diagnostic when:
- Code outside a feature tries to reference or import a type from the feature's internal namespace
- The internal namespace is defined with the feature-internal naming convention

## Why This Matters

### Enforcing Architectural Boundaries

Feature-internal namespaces define the internal API of a feature:
- They help maintain clean architecture by separating public contracts from implementation details
- They prevent accidental coupling between features through private implementation
- They make refactoring safer since internal details can change without affecting external code
- They establish clear ownership of code across different features

Enforcing these boundaries with analyzers ensures your architecture stays clean as the codebase grows.

## How to Fix

### Option 1: Use the public API

If you need functionality from a feature, use its public namespace:

```csharp
// ❌ WRONG - Referencing internal namespace
using Users.Internal;

var user = new InternalUserHelper().GetUser(id);

// ✅ CORRECT - Using public API
using Users;

var user = new UserService().GetUser(id);
```

### Option 2: Move code into the feature

If you're working on the same feature, put your code in the correct location:

```csharp
// ❌ WRONG - Outside the feature accessing internal namespace
// File: Orders/OrderProcessor.cs
using Users.Internal;

// ✅ CORRECT - Within the feature
// File: Users/Internal/UserHelper.cs
// or
// File: Users/UserService.cs (public API)
```

### Option 3: Promote to public API

If functionality should be shared, move it from the internal namespace to the public API:

```csharp
// ❌ WRONG - Kept as internal
// File: Users/Internal/UserValidator.cs
internal static class UserValidator { }

// ✅ CORRECT - Promoted to public
// File: Users/UserValidator.cs
public static class UserValidator { }
```

## Example

```csharp
// File: Users/Internal/InternalUserCache.cs
namespace Users.Internal;

internal class InternalUserCache
{
    // Implementation details...
}

// File: Orders/OrderService.cs
namespace Orders;

// ⚠️ SW101 - Referencing internal namespace
using Users.Internal;

public class OrderService
{
    private InternalUserCache cache = new();  // ❌ Violates SW101
}

// ✅ CORRECT - Use public API
using Users;

public class OrderService
{
    private IUserService userService;
}
```

## Feature Internal Namespace Naming

Internal namespaces typically follow this pattern:

```
FeatureName.Internal
FeatureName.Internal.Implementation
FeatureName.Internal.Helpers
```

For example:
- `PaymentProcessing.Internal`
- `Users.Internal.Security`
- `Inventory.Internal.Cache`

## Configuration

This rule is currently disabled to avoid false positives. To enable it:

```ini
[*.cs]
dotnet_diagnostic.SW101.severity = warning
```

To treat violations as errors:

```ini
[*.cs]
dotnet_diagnostic.SW101.severity = error
```

## Related Rules

- [SW102 - Forbidden using to feature-internal namespace](./sw102-forbidden-namespace-using)

## See Also

- Clean Architecture principles
- Feature-driven development patterns
- Namespace organization best practices
