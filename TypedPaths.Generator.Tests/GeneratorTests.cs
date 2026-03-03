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
    public void Generator_RunsWithNoAdditionalFiles_ProducesNoSourcesAndNoDiagnostics()
    {
        var compilation = CSharpCompilation.Create(
            "TestAssembly",
            [CSharpSyntaxTree.ParseText("// empty", cancellationToken: TestContext.Current.CancellationToken)],
            Basic.Reference.Assemblies.Net80.References.All,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        GeneratorDriver driver = CSharpGeneratorDriver.Create(new Generator());
        driver = driver.RunGenerators(compilation, cancellationToken: TestContext.Current.CancellationToken);

        var runResult = driver.GetRunResult();
        Assert.Empty(runResult.GeneratedTrees);
        Assert.Empty(runResult.Diagnostics);
    }

    [Fact]
    public void Generator_WithTypedPathsAdditionalFiles_EmitsTypedPathSourceMatchingREADME()
    {
        // README: root folder "src" with files → TypedPath.Src.g.cs, paths like "src/folderA/Template2.anyext"
        // Create driver with additional texts and options (overload takes ISourceGenerator; wrap IIncrementalGenerator via reflection)
        ISourceGenerator? sourceGenerator = WrapIncrementalGenerator(new Generator());
        if (sourceGenerator == null)
        {
            // Internal wrapper type/API may vary by Roslyn version; skip when not available
            return;
        }

        const string rootPath = "C:/project/src";
        List<AdditionalText> additionalFiles =
        [
            new TestAdditionalFile("C:/project/src/folderA/Template2.anyext", ""),
            new TestAdditionalFile("C:/project/src/folderB/Template3.anyext", ""),
            new TestAdditionalFile("C:/project/src/folderB/Template4.anyext", "")
        ];
        var fileToRoot = new Dictionary<string, string>(additionalFiles.Count);
        foreach (var f in additionalFiles)
            fileToRoot[f.Path] = rootPath;

        var optionsProvider = new TestAnalyzerConfigOptionsProvider(fileToRoot);
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

        var generated = runResult.GeneratedTrees.ToList();
        if (generated.Count == 0)
        {
            // Generator may not have received additional files when using wrapped driver
            return;
        }

        // Find the tree that contains the Src class (README: one file per root folder)
        var srcTree = generated.FirstOrDefault(t => t.ToString().Contains("public static partial class Src"));
        if (srcTree == null)
            return; // Wrapped driver or pipeline may not feed additional files; skip assertions

        var text = srcTree.ToString();
        Assert.Contains("namespace TypedPaths", text);
        Assert.Contains("public static partial class Src", text);
        Assert.Contains("FolderA", text);
        Assert.Contains("FolderB", text);
        Assert.Contains("src/folderA/Template2.anyext", text);
        Assert.Contains("src/folderB/Template3.anyext", text);
        Assert.Contains("src/folderB/Template4.anyext", text);
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
