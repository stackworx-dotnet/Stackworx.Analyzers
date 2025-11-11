# Read Me

![NuGet Version](https://img.shields.io/nuget/v/Stackworx.Analyzers)

Collection of Analyzers to improve code quality and avoid specific bugs

## Installation

```sh
dotnet add package Stackworx.Analyzers
```

```xml
<ItemGroup>
  <PackageReference Include="Stackworx.Analyzers" PrivateAssets="all" />
</ItemGroup>
```

There in your editorconfig add the feaure_namespaces key:

```.editorconfig
dotnet_code_quality.Stackworx.Analyzers.feature_namespaces = MyCompany.Features
```

## Requirements

The Feature linter only makes sense in combination with `IDE0161`