---
sidebar_position: 1
---

# Overview

This page provides a comprehensive table of all available Stackworx analyzers and their rules. Click on any rule to view detailed documentation.

## All Rules

| Rule ID                                                            | Title | Category | Severity | Summary |
|--------------------------------------------------------------------|-------|----------|----------|---------|
| [SW0001](./rules/sw0001-avoid-implicit-datetime-to-datetimeoffset) | Avoid implicit DateTime → DateTimeOffset conversion | Usage | Warning | Flags implicit conversions from DateTime to DateTimeOffset, requiring explicit construction. |
| [SWGQL01](./rules/swgql01-static-extension-method-validation)            | Field extension methods on non-static classes must be instance methods | GraphQL | Error | Validates that static field extension methods are only used in static classes. |
| [SWGQL02](./rules/swgql02-unused-dataloader-interface)                   | DataLoader interface appears unused | GraphQL | Info | Detects DataLoader interfaces that aren't referenced in the compilation. |
| [SWGQL03](./rules/swgql03-graphql-extension-class-static)                | Class with [ExtendObjectType] must be static | GraphQL | Error | Enforces that classes annotated with [ExtendObjectType] are declared as static. |
| [SW101](./rules/sw101-forbidden-namespace-reference)                     | Forbidden reference to feature-internal namespace | Architecture | Warning | Prevents references to feature-internal namespaces from outside the feature. |
| [SW102](./rules/sw102-forbidden-namespace-using)                         | Forbidden using to feature-internal namespace | Architecture | Warning | Prevents using directives that import feature-internal namespaces from outside. |

## Rules by Category

### GraphQL Rules
- [SWGQL01 - Field extension methods on non-static classes must be instance methods](./swgql01-static-extension-method-validation)
- [SWGQL02 - DataLoader interface appears unused](./swgql02-unused-dataloader-interface)
- [SWGQL03 - Class with [ExtendObjectType] must be static](./swgql03-graphql-extension-class-static)

### Usage Rules
- [SW0001 - Avoid implicit DateTime → DateTimeOffset conversion](./sw0001-avoid-implicit-datetime-to-datetimeoffset)

### Architecture Rules
- [SW101 - Forbidden reference to feature-internal namespace](./sw101-forbidden-namespace-reference)
- [SW102 - Forbidden using to feature-internal namespace](./sw102-forbidden-namespace-using)
