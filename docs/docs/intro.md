---
sidebar_position: 1
---

# Installation

Stackworx.Analyzers is a collection of Roslyn analyzers that help enforce architectural patterns and best practices in your C# projects.

## Quick Start

### Via CLI

```sh
dotnet add package Stackworx.Analyzers
```

### Via Project File

Add the package reference to your `.csproj` file:

```xml
<ItemGroup>
  <PackageReference Include="Stackworx.Analyzers" Version="*" PrivateAssets="all" />
</ItemGroup>
```

:::note
The `PrivateAssets="all"` attribute ensures the analyzer package is only used for analysis and isn't included as a dependency when your project is referenced by others.
:::

## Configuration

### EditorConfig

Some analyzers require configuration through your `.editorconfig` file. At minimum, you may want to configure feature namespaces if using the namespace-related analyzers:

```ini
# .editorconfig
[*.cs]
dotnet_code_quality.Stackworx.Analyzers.feature_namespaces = MyCompany.Features
```

Replace `MyCompany.Features` with your actual feature namespace root.

### Rule Configuration

You can enable, disable, or change the severity of individual rules. For example:

```ini
[*.cs]
# Disable a specific rule
dotnet_diagnostic.SW0001.severity = none

# Change a rule to error
dotnet_diagnostic.SWGQL03.severity = error

# Change a rule to warning (default)
dotnet_diagnostic.SWGQL01.severity = warning
```

## Verification

After installation, build your project to verify the analyzers are working:

```sh
dotnet build
```

You should see warnings or errors from the analyzers in your build output, depending on your code and the default severity of each rule.

## Next Steps

- Review the [Rules Overview](/docs/rules/overview) to understand what each analyzer does
- Check individual [rule documentation](/docs/rules/overview#all-rules) for detailed information
- Configure rules in your `.editorconfig` to match your project's requirements