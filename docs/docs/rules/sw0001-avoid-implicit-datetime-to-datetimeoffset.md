---
sidebar_position: 2
---

# SW0001: Avoid implicit DateTime → DateTimeOffset conversion

## Overview

**Rule ID:** SW0001  
**Category:** Usage  
**Severity:** Warning  
**Status:** Enabled by default

## Description

This rule flags implicit conversions from `System.DateTime` to `System.DateTimeOffset`. These implicit conversions can hide important semantic information about the offset and Kind properties of the DateTime value, leading to subtle bugs and unexpected behavior.

## When This Rule Triggers

The analyzer reports a diagnostic when:
- An implicit conversion from `DateTime` to `DateTimeOffset` is detected in your code
- The conversion is user code (not compiler-generated)

## Why This Matters

### The Problem

When you implicitly convert a `DateTime` to `DateTimeOffset`:

```csharp
DateTime dt = DateTime.Now;
DateTimeOffset dto = dt;  // ⚠️ Implicit conversion - SW0001
```

The conversion doesn't preserve or communicate intent about:
- What the original DateTime's `Kind` property is (Local, UTC, Unspecified)
- What offset should be assumed for the DateTimeOffset

This can lead to:
- Incorrect timezone handling
- Data loss or corruption when persisting values
- Subtle bugs that are hard to diagnose

## How to Fix

Make the conversion explicit using one of the `DateTimeOffset` constructors:

### For Local DateTime
```csharp
DateTime dt = DateTime.Now;
// ✅ Explicit conversion showing local time zone intent
DateTimeOffset dto = new DateTimeOffset(dt);
```

### For UTC DateTime
```csharp
DateTime dt = DateTime.UtcNow;
// ✅ Explicit conversion showing UTC intent
DateTimeOffset dto = new DateTimeOffset(dt, TimeSpan.Zero);
```

### For DateTime with specific offset
```csharp
DateTime dt = GetDateTime();
TimeSpan offset = TimeSpan.FromHours(-5);
// ✅ Explicit conversion with explicit offset
DateTimeOffset dto = new DateTimeOffset(dt, offset);
```

### For nullable DateTime
```csharp
DateTime? dt = DateTime.Now;
// ✅ Handle nullable explicitly
DateTimeOffset? dto = dt.HasValue 
    ? new DateTimeOffset(dt.Value) 
    : null;
```

## Configuration

This rule is enabled by default. To disable it, add to your `.editorconfig`:

```ini
[*.cs]
dotnet_diagnostic.SW0001.severity = none
```

To change severity to error:

```ini
[*.cs]
dotnet_diagnostic.SW0001.severity = error
```

## Related Rules

- None

## See Also

- [DateTime vs DateTimeOffset](https://learn.microsoft.com/en-us/dotnet/api/system.datetimeoffset)
- [DateTime.Kind property](https://learn.microsoft.com/en-us/dotnet/api/system.datetime.kind)
