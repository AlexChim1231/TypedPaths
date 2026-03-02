# TypedPaths.Generator

A Roslyn source generator that turns a folder tree into **strongly typed path constants** at compile time. Point it at a base path (e.g. `/src` or `/templates`), and get nested static classes and `const string` members you can use instead of magic strings.

## What you get

Given a structure like:

```
/src
  Template1.anyext
  folderA/
    Template2.anyext
  folderB/
    Template3.anyext
    Template4.anyext
```

the generator emits a single file `TypedPaths.g.cs` with:

```csharp
namespace TypedPaths;

public static class TypedPaths
{
    public static class Src
    {
        public const string Template1 = "src/Template1.anyext";
        public static class FolderA
        {
            public const string Template2 = "src/folderA/Template2.anyext";
        }
        public static class FolderB
        {
            public const string Template3 = "src/folderB/Template3.anyext";
            public const string Template4 = "src/folderB/Template4.anyext";
        }
    }
}
```

So you can use `TypedPaths.Src.FolderA.Template2` instead of `"src/folderA/Template2.anyext"` and keep paths refactor-safe and discoverable.

## Requirements

- .NET 8 (or the TFM your project uses; the generator targets .NET Standard 2.0)
- MSBuild / SDK-style projects

## Setup

1. **Reference the generator** (as an analyzer, not a normal assembly):

   ```xml
   <ItemGroup>
     <ProjectReference Include="path\to\TypedPaths.Generator\TypedPaths.Generator.csproj"
                       OutputItemType="Analyzer"
                       ReferenceOutputAssembly="false" />
   </ItemGroup>
   ```

2. **Set the base path** (any relative path you want to expose as typed paths):

   ```xml
   <PropertyGroup>
     <TypedPathsBasePath>/src</TypedPathsBasePath>
   </PropertyGroup>

   <ItemGroup>
     <CompilerVisibleProperty Include="TypedPathsBasePath" />
   </ItemGroup>
   ```

   Examples: `/src`, `src`, `/templates`, `/assets/email`.

3. **Include the files** under that path as `AdditionalFiles` so the generator can see them:

   ```xml
   <ItemGroup>
     <AdditionalFiles Include="src\**\*" />
   </ItemGroup>
   ```

Build the project; the generator runs and adds `TypedPaths.g.cs` to the compilation.

## Usage in code

The generated type is in namespace `TypedPaths`, and the root class is also named `TypedPaths`. To avoid name clashes, use an alias:

```csharp
using TypedPathsRoot = TypedPaths.TypedPaths;

// Use as const strings
string path = TypedPathsRoot.Src.FolderA.Template2;  // "src/folderA/Template2.anyext"

// e.g. resolve to full path
var fullPath = Path.Combine(projectRoot, TypedPathsRoot.Src.FolderA.Template2);
```

## Naming rules

- Folder and file names become **PascalCase** identifiers.
- Invalid identifier characters are dropped or split; leading digits get a `_` prefix.
- Duplicate names in the same scope get suffixes: `_2`, `_3`, etc.
- Extensions are stripped from member names but kept in the path string.

## Repository layout

| Project | Description |
|--------|-------------|
| `TypedPaths.Generator` | The source generator (Roslyn incremental generator). |
| `TypedPaths.Generator.Sample` | Example app that uses the generator and runs a small demo. |
| `TypedPaths.Generator.Tests` | Unit tests for the generator. |

## Build and test

```bash
dotnet restore
dotnet build
dotnet test
```

Run the sample:

```bash
dotnet run --project TypedPaths.Generator.Sample
```

## License

See the repository for license information.
