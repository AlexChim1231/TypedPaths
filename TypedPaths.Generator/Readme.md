# Typed Paths Generator

Generate strongly typed constants for project file paths at compile time.

## What It Generates

If your project includes files under a configured base path (for example `/src`), this generator emits a `TypedPaths.g.cs` file with nested static classes:

```csharp
TypedPaths.Src.Template1
TypedPaths.Src.FolderA.Template2
TypedPaths.Src.FolderB.Template3
```

Each member is a `const string` value containing a normalized relative path:

```csharp
TypedPaths.Src.FolderA.Template2 == "src/folderA/Template2.anyext"
```

## Setup

In your consumer project:

```xml
<ItemGroup>
  <ProjectReference Include="..\TypedPaths.Generator\TypedPaths.Generator.csproj"
                    OutputItemType="Analyzer"
                    ReferenceOutputAssembly="false" />
</ItemGroup>

<PropertyGroup>
  <TypedPathsBasePath>/src</TypedPathsBasePath>
</PropertyGroup>

<ItemGroup>
  <CompilerVisibleProperty Include="TypedPathsBasePath" />
</ItemGroup>

<ItemGroup>
  <AdditionalFiles Include="src\**\*" />
</ItemGroup>
```

Build the project to trigger source generation in the IDE/compiler.

`TypedPathsBasePath` accepts any relative path such as `/src`, `src`, `/templates`, or `/assets/email`.

## Naming Rules

- Folder and file names are converted to PascalCase identifiers.
- Invalid identifier characters are removed/split.
- Names starting with a digit are prefixed with `_`.
- Collisions in the same scope get deterministic suffixes: `_2`, `_3`, etc.
- File extensions are removed from member names, but preserved in path values.

## Current Scope

- Scans files under the configured base path from `TypedPathsBasePath`.
- Paths are normalized to `/` separators in generated constants.
- Files outside the configured base path are ignored.

## Projects

- `TypedPaths.Generator`: Roslyn source generators.
- `TypedPaths.Generator.Sample`: minimal consumer/demo usage.
- `TypedPaths.Generator.Tests`: unit tests for generation behavior.