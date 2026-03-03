using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

using TypedPaths.Generator.Tests.Utils;

using Xunit;

namespace TypedPaths.Generator.Tests;

/// <summary>
/// Tests for the TypedPaths source generator. Usage and output shape are documented in README.
/// </summary>
public class GeneratorTests
{
    [Fact]
    public void Generator_RunsWithNoAdditionalFiles_ProducesOnlyAttributeSourceAndNoDiagnostics()
    {
        var compilation = CSharpCompilation.Create(
            "TestAssembly",
            [CSharpSyntaxTree.ParseText("// empty", cancellationToken: TestContext.Current.CancellationToken)],
            Basic.Reference.Assemblies.Net80.References.All,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        GeneratorDriver driver = CSharpGeneratorDriver.Create(new Generator());
        driver = driver.RunGenerators(compilation, cancellationToken: TestContext.Current.CancellationToken);

        var runResult = driver.GetRunResult();
        var generatedTrees = runResult.GeneratedTrees;
        var generatedSource = Assert.Single(generatedTrees).ToString();
        Assert.Contains("internal class TypedPathAttribute : System.Attribute", generatedSource);
        Assert.Empty(runResult.Diagnostics);
    }

    [Fact]
    public void Generator_WithTypedPathsAdditionalFiles_EmitsProjectRelativeValues()
    {
        ISourceGenerator? sourceGenerator = WrapIncrementalGenerator(new Generator());
        if (sourceGenerator == null)
        {
            return;
        }

        const string projectDir = "C:/project";
        const string rootPath = "C:/project/src";
        List<AdditionalText> additionalFiles =
        [
            new TestAdditionalFile("C:/project/src/Template1.anyext", ""),
            new TestAdditionalFile("C:/project/src/folderA/Template2.anyext", ""),
            new TestAdditionalFile("C:/project/src/folderB/Template3.anyext", ""),
            new TestAdditionalFile("C:/project/src/folderB/Template4.anyext", "")
        ];

        var fileMetadata = new Dictionary<string, TestAnalyzerConfigOptionsProvider.FileMetadata>(additionalFiles.Count);
        foreach (var f in additionalFiles)
        {
            fileMetadata[f.Path] = new TestAnalyzerConfigOptionsProvider.FileMetadata(
                RootPath: rootPath,
                ClassName: "Src",
                ProjectDirectory: projectDir);
        }

        var optionsProvider = new TestAnalyzerConfigOptionsProvider(fileMetadata);
        var compilation = CSharpCompilation.Create(
            "TestAssembly",
            [CSharpSyntaxTree.ParseText("// empty", cancellationToken: TestContext.Current.CancellationToken)],
            Basic.Reference.Assemblies.Net80.References.All,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        GeneratorDriver driver = CSharpGeneratorDriver.Create(
            [sourceGenerator],
            additionalFiles,
            null,
            optionsProvider);
        driver = driver.RunGenerators(compilation, cancellationToken: TestContext.Current.CancellationToken);

        var runResult = driver.GetRunResult();
        Assert.Empty(runResult.Diagnostics);

        var srcTree = runResult.GeneratedTrees.FirstOrDefault();
        Assert.NotNull(srcTree);

        var text = srcTree!.ToString();
        Assert.Contains("namespace TypedPaths", text);
        Assert.Contains("public static partial class TypedPaths", text);
        Assert.Contains("public static partial class Src", text);
        Assert.Contains("public const string Value = \"src\";", text);
        Assert.Contains("FolderA", text);
        Assert.Contains("FolderB", text);
        Assert.Contains("Template1", text);
        Assert.Contains("src/folderA/Template2.anyext", text);
        Assert.Contains("src/folderB/Template3.anyext", text);
        Assert.Contains("src/folderB/Template4.anyext", text);
    }

    [Fact]
    public void Generator_WhenFolderAndFileNamesConflict_FolderKeepsNameAndFileGetsExtensionSuffix()
    {
        ISourceGenerator? sourceGenerator = WrapIncrementalGenerator(new Generator());
        if (sourceGenerator == null)
        {
            return;
        }

        const string projectDir = "C:/project";
        const string rootPath = "C:/project/src";
        List<AdditionalText> additionalFiles =
        [
            new TestAdditionalFile("C:/project/src/report.txt", ""),
            new TestAdditionalFile("C:/project/src/report/child.anyext", "")
        ];

        var fileMetadata = additionalFiles.ToDictionary(
            f => f.Path,
            _ => new TestAnalyzerConfigOptionsProvider.FileMetadata(rootPath, "Src", projectDir),
            StringComparer.OrdinalIgnoreCase);
        var optionsProvider = new TestAnalyzerConfigOptionsProvider(fileMetadata);

        var compilation = CSharpCompilation.Create(
            "TestAssembly",
            [CSharpSyntaxTree.ParseText("// empty", cancellationToken: TestContext.Current.CancellationToken)],
            Basic.Reference.Assemblies.Net80.References.All,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        GeneratorDriver driver = CSharpGeneratorDriver.Create(
            [sourceGenerator],
            additionalFiles,
            null,
            optionsProvider);
        driver = driver.RunGenerators(compilation, cancellationToken: TestContext.Current.CancellationToken);

        var runResult = driver.GetRunResult();
        Assert.Empty(runResult.Diagnostics);
        var text = runResult.GeneratedTrees.Single().ToString();

        Assert.Contains("public static partial class Report", text);
        Assert.Contains("public const string Value = \"src/report\";", text);
        Assert.Contains("public static partial class ReportTxt", text);
        Assert.Contains("public const string Value = \"src/report.txt\";", text);
    }

    [Fact]
    public void Generator_WhenExtensionlessFileConflictsWithFolder_UsesFileSuffix()
    {
        ISourceGenerator? sourceGenerator = WrapIncrementalGenerator(new Generator());
        if (sourceGenerator == null)
        {
            return;
        }

        const string projectDir = "C:/project";
        const string rootPath = "C:/project/src";
        List<AdditionalText> additionalFiles =
        [
            new TestAdditionalFile("C:/project/src/data", ""),
            new TestAdditionalFile("C:/project/src/data/child.anyext", "")
        ];

        var fileMetadata = additionalFiles.ToDictionary(
            f => f.Path,
            _ => new TestAnalyzerConfigOptionsProvider.FileMetadata(rootPath, "Src", projectDir),
            StringComparer.OrdinalIgnoreCase);
        var optionsProvider = new TestAnalyzerConfigOptionsProvider(fileMetadata);

        var compilation = CSharpCompilation.Create(
            "TestAssembly",
            [CSharpSyntaxTree.ParseText("// empty", cancellationToken: TestContext.Current.CancellationToken)],
            Basic.Reference.Assemblies.Net80.References.All,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        GeneratorDriver driver = CSharpGeneratorDriver.Create(
            [sourceGenerator],
            additionalFiles,
            null,
            optionsProvider);
        driver = driver.RunGenerators(compilation, cancellationToken: TestContext.Current.CancellationToken);

        var runResult = driver.GetRunResult();
        Assert.Empty(runResult.Diagnostics);
        var text = runResult.GeneratedTrees.Single().ToString();

        Assert.Contains("public static partial class Data", text);
        Assert.Contains("public static partial class DataFile", text);
        Assert.Contains("public const string Value = \"src/data\";", text);
    }

    /// <summary>
    /// Roslyn's CSharpGeneratorDriver.Create(ISourceGenerator[], ...) requires ISourceGenerator.
    /// The driver internally wraps IIncrementalGenerator when using Create(IIncrementalGenerator[]),
    /// but that overload does not accept additional texts. We use reflection to create the same
    /// internal wrapper so we can test with AdditionalTexts and AnalyzerConfigOptionsProvider.
    /// </summary>
    private static ISourceGenerator? WrapIncrementalGenerator(IIncrementalGenerator incrementalGenerator)
    {
        var csharpAssembly = typeof(CSharpCompilation).Assembly;
        Type[] types;
        try
        {
            types = csharpAssembly.GetTypes();
        }
        catch (ReflectionTypeLoadException ex)
        {
            types = ex.Types.Where(x => x != null).ToArray()!;
        }

        foreach (var t in types)
        {
            if (t.IsAbstract || !typeof(ISourceGenerator).IsAssignableFrom(t))
                continue;
            var ctor = t.GetConstructors(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                .FirstOrDefault(c => c.GetParameters().Length == 1
                    && c.GetParameters()[0].ParameterType == typeof(IIncrementalGenerator));
            if (ctor != null)
            {
                return (ISourceGenerator)Activator.CreateInstance(t, incrementalGenerator)!;
            }
        }
        return null;
    }
}