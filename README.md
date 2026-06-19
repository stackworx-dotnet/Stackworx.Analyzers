# Stackworx.Analyzers

[![NuGet Version](https://img.shields.io/nuget/v/Stackworx.Analyzers)](https://www.nuget.org/packages/Stackworx.Analyzers/)

Collection of Roslyn analyzers to improve code quality and avoid specific bugs.

- Documentation website: https://stackworx-dotnet.github.io/Stackworx.Analyzers/
- Package README (and full documentation): [`docs/README.md`](docs/README.md)

## Installation

```sh
dotnet add package Stackworx.Analyzers
```

```xml
<ItemGroup>
    <PackageReference Include="Stackworx.Analyzers" Version="*" OutputItemType="Analyzer" ReferenceOutputAssembly="false" />
</ItemGroup>
```

## Testing on a local project

To try a local build of the analyzer in another solution before publishing, pack it into a local
NuGet package and consume it from a private feed.

### 1. Pack a local build

Pack the analyzer into a `.nupkg`. Override the version so it sorts above the published one and is
easy to spot:

```sh
dotnet pack Stackworx.Analyzers -c Release -o ./local-nuget -p:Version=0.0.1-local.1
```

The `.csproj` already maps the built assembly into `analyzers/dotnet/cs`, so the resulting package
behaves exactly like the one on NuGet.org.

### 2. Add the folder as a NuGet source

In the consuming project (or globally), register the output folder as a package source. Either add a
`nuget.config` next to the consuming solution:

```xml
<configuration>
  <packageSources>
    <add key="stackworx-local" value="/absolute/path/to/Stackworx.Analyzers/local-nuget" />
  </packageSources>
</configuration>
```

…or add it from the CLI:

```sh
dotnet nuget add source /absolute/path/to/Stackworx.Analyzers/local-nuget --name stackworx-local
```

### 3. Reference it

```xml
<ItemGroup>
    <PackageReference Include="Stackworx.Analyzers" Version="0.0.1-local.1" OutputItemType="Analyzer" ReferenceOutputAssembly="false" />
</ItemGroup>
```

> NuGet caches packages by version. When iterating, bump the version on each `pack` (e.g.
> `-local.2`, `-local.3`) — reusing a version means the old cached copy is used. If you must reuse a
> version, clear the cache first with `dotnet nuget locals global-packages --clear`.

### Faster loop: ProjectReference

If the project under test lives in the same solution (or you can reference the source directly), skip
packing entirely and reference the analyzer project — this is how
[`Stackworx.Analyzers.Sample`](Stackworx.Analyzers.Sample) consumes it:

```xml
<ItemGroup>
   <PackageReference Include="Stackworx.Analyzers" PrivateAssets="all" />
</ItemGroup>
```

After changing analyzer code, rebuild and restart the IDE's language server (or reload the window)
so it picks up the new analyzer assembly.
