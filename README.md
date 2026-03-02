# TypedPaths.Generator

A Roslyn source generator that turns configured folder trees into **strongly typed path constants** at compile time. Each configured folder becomes a nested static class (for example `Src`, `Template`) so you can avoid magic strings.

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
/template
  email/
    welcome.txt
  sms/
    otp.txt
```

the generator emits one file per root folder (`TypedPaths.Src.g.cs`, `TypedPaths.Template.g.cs`, ...), each contributing to the same partial root class:

```csharp
// TypedPaths.Src.g.cs
namespace TypedPaths;

public static partial class TypedPaths
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

```csharp
// TypedPaths.Template.g.cs
namespace TypedPaths;

public static partial class TypedPaths
{
    public static class Template
    {
        public static class Email
        {
            public const string Welcome = "template/email/welcome.txt";
        }
        public static class Sms
        {
            public const string Otp = "template/sms/otp.txt";
        }
    }
}
```

So you can use `TypedPaths.Src.FolderA.Template2` and `TypedPaths.Template.Email.Welcome` instead of raw string paths.

## Requirements

- .NET 8 (or the TFM your project uses; the generator targets .NET Standard 2.0)
- MSBuild / SDK-style projects

## Setup

### Option A: consume from NuGet (recommended)

```xml
<ItemGroup>
  <PackageReference Include="TypedPaths.Generator" Version="1.0.0" />
</ItemGroup>

<ItemGroup>
  <TypedPathsFolder Include="/src" ClassName="Src" />
  <TypedPathsFolder Include="/template" />
</ItemGroup>
```

`TypedPaths.Generator.targets` (from the package `buildTransitive` assets) maps these folders to `AdditionalFiles` and `Content` items for generation.

### Option B: local project reference (repository development)

```xml
<ItemGroup>
  <ProjectReference Include="..\TypedPaths.Generator\TypedPaths.Generator.csproj"
                    OutputItemType="Analyzer"
                    ReferenceOutputAssembly="false" />
</ItemGroup>

<ItemGroup>
  <TypedPathsFolder Include="/src" ClassName="Src" />
  <TypedPathsFolder Include="/template" />
</ItemGroup>

<Import Project="..\TypedPaths.Generator\buildTransitive\TypedPaths.Generator.targets"
        Condition="Exists('..\TypedPaths.Generator\buildTransitive\TypedPaths.Generator.targets')" />
```

The explicit `<Import />` is important for local project reference scenarios so folder declarations are translated into `AdditionalFiles` consistently during local builds.

Build the project; the generator runs and adds `TypedPaths.*.g.cs` files to the compilation.

## Usage in code

The generated type is in namespace `TypedPaths`, and the root class is also named `TypedPaths`. To avoid name clashes, use an alias:

```csharp
using TypedPathsRoot = TypedPaths.TypedPaths;

// Use as const strings
string path = TypedPathsRoot.Src.FolderA.Template2;  // "src/folderA/Template2.anyext"
string emailTemplate = TypedPathsRoot.Template.Email.Welcome; // "template/email/welcome.txt"

// e.g. resolve to full path
var fullPath = Path.Combine(projectRoot, TypedPathsRoot.Src.FolderA.Template2);
```

## Naming rules

- Folder and file names become **PascalCase** identifiers.
- Invalid identifier characters are dropped or split; leading digits get a `_` prefix.
- Duplicate names in the same scope get suffixes: `_2`, `_3`, etc.
- Extensions are stripped from member names but kept in the path string.

## Repository layout


| Project                       | Description                                                |
| ----------------------------- | ---------------------------------------------------------- |
| `TypedPaths.Generator`        | The source generator (Roslyn incremental generator).       |
| `TypedPaths.Generator.Sample` | Example app that uses the generator and runs a small demo. |
| `TypedPaths.Generator.Tests`  | Unit tests for the generator.                              |


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

See the repository for [license](LICENSE) information.