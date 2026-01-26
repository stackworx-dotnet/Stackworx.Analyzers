---
sidebar_position: 3
---

# SW002: Method appears unused

## Overview

**Rule ID:** SW002  
**Category:** Usage  
**Severity:** Warning  
**Status:** Disabled by default

## Description

This rule flags *source-declared* methods that have **no call sites / references in the current compilation**.

It’s meant to help you find dead code after refactoring, especially in codebases where features are mostly self-contained.

## When This Rule Triggers

The analyzer reports a diagnostic when all of the following are true:

- The method is a **normal method** (`MethodKind.Ordinary`) declared in source (not compiler generated)
- The method is **not** an override
- The method is **not** an explicit interface implementation
- The method is **not** referenced via:
  - a direct invocation (`obj.M()` / `M()`), or
  - a method-group / delegate reference (`Action a = M;`)  
  within the same compilation

### Automatic ignore via JetBrains annotations

If any of the following symbols are annotated, the method is ignored:

- The method itself, **or**
- Its containing type, **or**
- Any outer containing type (for nested types)

Supported attribute names:
- `JetBrains.Annotations.PublicAPIAttribute` / `PublicAPI`
- `JetBrains.Annotations.UsedImplicitlyAttribute` / `UsedImplicitly`

## Why This Matters

Unused methods tend to:

- Make features harder to understand (“Can I delete this?”)
- Increase maintenance cost and cognitive load
- Hide real intent (people are afraid to refactor if too much dead code accumulates)

## Examples

### Reported (unused method)

```csharp
public class C
{
    void Dead() { } // SW002
}
```

### Not reported (method referenced)

```csharp
public class C
{
    void Alive() { }

    void Caller()
    {
        Alive();
    }
}
```

### Not reported (explicit opt-out)

```csharp
using JetBrains.Annotations;

public class C
{
    [PublicAPI]
    void CalledBySomeoneElse() { }
}
```

## Caveats / When SW002 is (and isn’t) a good fit

SW002 is intentionally simple: it only knows what can be proven *inside the compilation being analyzed*. That makes it powerful in the right context, and noisy in others.

- **Monorepos / large solution graphs**
  - SW002 doesn’t do “whole repo reachability”. It only sees references inside the project/compilation currently being analyzed.
  - In big solutions, you’ll often have shared projects where methods are only called by *other* projects. Those can look “unused” from the producing project’s point of view.

- **Apps / services (not libraries) vs. published libraries**
  - This rule fits best when the project is an *application* where most code is expected to be used internally.
  - In libraries meant to be consumed by external code, many (especially `public`) methods can appear unused in the library compilation but are still valid API.

- **No `InternalsVisibleTo`, reflection, DI, source generators, dynamic invocation**
  - If methods are reached indirectly (reflection, frameworks calling by convention, dependency injection, test projects using `InternalsVisibleTo`, etc.), SW002 may produce false positives.
  - Recommendation: mark those methods/types with `[PublicAPI]` / `[UsedImplicitly]`, or disable SW002 for those files/folders.

- **Works best with Feature Folders**
  - If your solution organizes code by “feature folders” (vertical slices) and keeps cross-feature dependencies minimal, SW002 becomes a practical way to keep each feature clean.
  - In this setup, unused methods are usually truly dead because the feature’s call graph tends to be contained.

## Configuration

This rule is **disabled by default**. To enable it:

```ini
[*.cs]
dotnet_diagnostic.SW002.severity = warning
```

To disable it (after enabling globally), or to silence it in specific areas:

```ini
[*.cs]
dotnet_diagnostic.SW002.severity = none
```

To treat it as an error:

```ini
[*.cs]
dotnet_diagnostic.SW002.severity = error
```

## Related Rules

- None

