---
sidebar_position: 1
---

# Overview

This page provides a comprehensive table of all available Stackworx analyzers and their rules. Click on any rule to view detailed documentation.

## All Rules

| Rule ID                                                    | Title | Category | Severity | Summary |
|------------------------------------------------------------|-------|----------|----------|---------|
| [SW001](./sw001-avoid-implicit-datetime-to-datetimeoffset) | Avoid implicit DateTime → DateTimeOffset conversion | Usage | Warning | Flags implicit conversions from DateTime to DateTimeOffset, requiring explicit construction. |
| [SW002](./sw002-unused-method)                             | Method appears unused | Usage | Warning (disabled by default) | Flags source-declared methods that have no references in the current compilation (ignores JetBrains [PublicAPI]/[UsedImplicitly]). |
| [SWGQL01](./swgql01-static-extension-method-validation)    | Field extension methods on non-static classes must be instance methods | GraphQL | Error | Validates that static field extension methods are only used in static classes. |
| [SWGQL02](./swgql02-unused-dataloader-interface)           | DataLoader interface appears unused | GraphQL | Info | Detects DataLoader interfaces that aren't referenced in the compilation. |
| [SWGQL03](./swgql03-graphql-extension-class-static)        | Class with [ExtendObjectType] must be static | GraphQL | Error | Enforces that classes annotated with [ExtendObjectType] are declared as static. |
| [SWGQL04](./swgql04-graphql-duplicate-extension-field)     | Duplicate GraphQL extension field | GraphQL | Error | Detects duplicate GraphQL field resolvers across `[ExtendObjectType<T>]` extension classes for the same type. |
| [SWGQL05](./swgql05-06-hotchocolate-types-usedimplicitly)  | HotChocolate GraphQL type should be marked as [UsedImplicitly] | GraphQL | Warning | GraphQL types are often discovered via reflection; requires [UsedImplicitly] on HotChocolate schema types. |
| [SWGQL06](./swgql05-06-hotchocolate-types-usedimplicitly)  | [UsedImplicitly] applied to non-GraphQL type | GraphQL | Warning | Flags [UsedImplicitly] on types that don't look like HotChocolate GraphQL schema types. |
| [SW101](./sw101-forbidden-namespace-reference)             | Forbidden reference to feature-internal namespace | Architecture | Warning | Prevents references to feature-internal namespaces from outside the feature. |
| [SW102](./sw102-forbidden-namespace-using)                 | Forbidden using to feature-internal namespace | Architecture | Warning | Prevents using directives that import feature-internal namespaces from outside. |

## Rules by Category

### GraphQL Rules
- [SWGQL01 - Field extension methods on non-static classes must be instance methods](./swgql01-static-extension-method-validation)
- [SWGQL02 - DataLoader interface appears unused](./swgql02-unused-dataloader-interface)
- [SWGQL03 - Class with [ExtendObjectType] must be static](./swgql03-graphql-extension-class-static)
- [SWGQL04 - Duplicate GraphQL extension field](./swgql04-graphql-duplicate-extension-field)
- [SWGQL05 / SWGQL06 - HotChocolate GraphQL types and [UsedImplicitly]](./swgql05-06-hotchocolate-types-usedimplicitly)

### Usage Rules
- [SW001 - Avoid implicit DateTime → DateTimeOffset conversion](./sw001-avoid-implicit-datetime-to-datetimeoffset)
- [SW002 - Method appears unused](./sw002-unused-method)

### Architecture Rules
- [SW101 - Forbidden reference to feature-internal namespace](./sw101-forbidden-namespace-reference)
- [SW102 - Forbidden using to feature-internal namespace](./sw102-forbidden-namespace-using)
