---
sidebar_position: 10
---

# SWBLZ01: Blazor component appears unused

## Overview

**Rule ID:** SWBLZ01  
**Category:** Blazor  
**Severity:** Warning  
**Status:** Enabled by default

## Description

This rule flags classes deriving from `Microsoft.AspNetCore.Components.ComponentBase` that are **never referenced anywhere in the current compilation**.

Because the Razor compiler emits the markup usage of a component (`<Foo />`) as a `builder.OpenComponent<Foo>(...)` call in generated C#, a component that is rendered by any other component — or referenced as a plain type — is considered used.

## When This Rule Triggers

The analyzer reports a diagnostic when all of the following are true:

- The type is a **non-abstract, non-static class** declared in source
- It **derives from** `Microsoft.AspNetCore.Components.ComponentBase`
- It is **not routable** — it has no `@page` directive (which emits `[Microsoft.AspNetCore.Components.RouteAttribute]`)
- No type reference to it exists anywhere in the compilation, including:
  - rendering via markup (`<Component />` → `OpenComponent<Component>()`)
  - use as a layout (`@layout Component` → `[Layout(typeof(Component))]`)
  - use as a base class of another component
  - any other type reference (fields, `typeof`, type arguments, etc.)

### Why `@page` components are ignored

Routable components are reachable through the router by their route template, so they typically have no direct type reference in the compilation. Flagging them would be a false positive, so they are excluded.

### Layout components are ignored

Components deriving from `Microsoft.AspNetCore.Components.LayoutComponentBase` are excluded for the same reason. A layout is selected **by name** — through an `@layout` directive, `_Imports.razor`, or a router `DefaultLayout` — and is frequently defined in a shared Razor Class Library and consumed from another assembly. Like routable pages, it has no reliable direct type reference in its own compilation, so it is never flagged.

### Generated components are ignored

Components stamped with `[System.CodeDom.Compiler.GeneratedCode]` — for example those emitted by **StrawberryShake**, NSwag, or Refit — are skipped. They are not user-editable and are often consumed only at runtime, so reporting them would be noise.

This does **not** affect your own components: the Razor compiler does not place `[GeneratedCode]` on the component classes it generates, so authored `.razor` components remain in scope.

### Automatic ignore via JetBrains annotations

If the component (or its declaration) is annotated with either of the following, the diagnostic is suppressed:

- `JetBrains.Annotations.UsedImplicitlyAttribute` / `UsedImplicitly`
- `JetBrains.Annotations.PublicAPIAttribute` / `PublicAPI`

This is the escape hatch for components reached in ways the analyzer can't see — dynamic rendering, reflection, DI, or consumption from another project.

Because a `.razor` file cannot carry attributes, annotate via a **code-behind partial class**. This is the standard way to opt out the root `App` / `Routes` components, which are referenced from the server project (a different assembly) and therefore look unused in their own compilation:

```csharp
// Routes.razor.cs
using JetBrains.Annotations;

[UsedImplicitly]
public partial class Routes;
```

The `[UsedImplicitly]` and the `ComponentBase` base type land on different partial declarations, but the compiler aggregates attributes across all partials, so the opt-out is honoured.

## Why This Matters

Orphaned components accumulate after refactors and feature removals. They:

- Make the component tree harder to reason about
- Increase build time and maintenance cost
- Hide intent — people hesitate to delete UI they aren't sure is dead

## Examples

### Reported (unused component)

```csharp
public class Orphan : ComponentBase { } // SWBLZ01
```

### Not reported (rendered by another component)

```razor
@* Parent.razor *@
<Child />
```
```csharp
public class Child : ComponentBase { } // referenced via OpenComponent<Child>
```

### Not reported (routable)

```razor
@page "/counter"
```
```csharp
[Route("/counter")]
public class Counter : ComponentBase { } // reachable via routing
```

### Not reported (explicit opt-out)

```csharp
using JetBrains.Annotations;

[UsedImplicitly]
public class DynamicallyRendered : ComponentBase { } // rendered via DynamicComponent / reflection
```

## Caveats

SWBLZ01 only knows what can be proven *inside the compilation being analyzed*.

- **Dynamic rendering** — components rendered via `DynamicComponent` (with a `Type` resolved at runtime) or by string name cannot be detected and may produce false positives.
- **Cross-project usage** — a shared component library whose components are only consumed by *other* projects will look unused from the library's own compilation.
- **Reflection / DI** — components reached indirectly are not seen.

Recommendation: mark those components with `[UsedImplicitly]` / `[PublicAPI]`, suppress the diagnostic, or disable the rule for shared component libraries.

## Configuration

This rule is **enabled by default**. To change its severity:

```ini
[*.cs]
dotnet_diagnostic.SWBLZ01.severity = none
```

## Related Rules

- [SW002 - Method appears unused](./sw002-unused-method)
- [SWGQL02 - DataLoader interface appears unused](./swgql02-unused-dataloader-interface)
