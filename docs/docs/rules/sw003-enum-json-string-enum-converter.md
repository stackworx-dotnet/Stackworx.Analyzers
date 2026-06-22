---
sidebar_position: 4
---

# SW003: Enum must have JsonStringEnumConverter

## Overview

**Rule ID:** SW003  
**Category:** Serialization  
**Severity:** Warning  
**Status:** Enabled by default

## Description

This rule ensures that all enums defined in user code are annotated with `[JsonConverter(typeof(JsonStringEnumConverter))]`. Without this attribute, `System.Text.Json` serializes enums as their numeric (integer) values by default, which can lead to brittle APIs and data that is difficult to read or debug.

## When This Rule Triggers

The analyzer reports a diagnostic when:
- An enum is declared without the `[JsonConverter(typeof(JsonStringEnumConverter))]` attribute
- `System.Text.Json` is referenced by the project

## Why This Matters

### The Problem

By default, `System.Text.Json` serializes enums as integers:

```csharp
// ⚠️ Missing attribute - SW003
public enum Status
{
    Active,   // serializes as 0
    Inactive  // serializes as 1
}
```

This results in JSON like `{"status": 0}`, which is hard to read, fragile (adding enum members can break consumers), and not self-documenting.

### The Solution

Annotating enums with `[JsonConverter(typeof(JsonStringEnumConverter))]` ensures they are serialized as their string names:

```csharp
// ✅ Correct
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum Status
{
    Active,   // serializes as "Active"
    Inactive  // serializes as "Inactive"
}
```

This produces `{"status": "Active"}`, which is readable, stable, and self-documenting.

## How to Fix

Add the `[JsonConverter(typeof(JsonStringEnumConverter))]` attribute to your enum:

```csharp
using System.Text.Json.Serialization;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum OrderStatus
{
    Pending,
    Processing,
    Shipped,
    Delivered
}
```

## Configuration

This rule is enabled by default. To disable it, add to your `.editorconfig`:

```ini
[*.cs]
dotnet_diagnostic.SW003.severity = none
```

To change severity to error:

```ini
[*.cs]
dotnet_diagnostic.SW003.severity = error
```

## Related Rules

- None

## See Also

- [JsonStringEnumConverter](https://learn.microsoft.com/en-us/dotnet/api/system.text.json.serialization.jsonstringenumconverter)
- [How to serialize and deserialize (marshal and unmarshal) JSON in .NET](https://learn.microsoft.com/en-us/dotnet/standard/serialization/system-text-json/overview)
