using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;

using TypedPaths.Generator.Tests.Utils;

using Xunit;

namespace TypedPaths.Generator.Tests;

public class SourceGeneratorWithAdditionalFilesTests
{
    private const string FolderIncludeMetadataKey = "build_metadata.AdditionalFiles.TypedPathsFolderInclude";
    private const string FolderClassNameMetadataKey = "build_metadata.AdditionalFiles.TypedPathsClassName";
    private const string FolderIncludeMetadataKeyLower = "build_metadata.additionalfiles.typedpathsfolderinclude";
    private const string FolderClassNameMetadataKeyLower = "build_metadata.additionalfiles.typedpathsclassname";

    [Fact]
    public void GeneratesNestedTypedPathsFromMultipleSourceRoots()
    {
        var generatedFiles = RunAndGetGeneratedFiles([
            new TestAdditionalFile("./src/Template1.anyext", "template"),
            new TestAdditionalFile("./src/folderA/Template2.anyext", "template"),
            new TestAdditionalFile("./src/folderB/Template3.anyext", "template"),
            new TestAdditionalFile("./src/folderB/Template4.anyext", "template"),
            new TestAdditionalFile("./template/email/welcome.txt", "template"),
            new TestAdditionalFile("./template/sms/otp.txt", "template")
        ], [
            new TypedPathsFolderConfig("/src", "Src"),
            new TypedPathsFolderConfig("/template")
        ]);

        var srcText = generatedFiles["TypedPaths.Src.g.cs"];
        Assert.Contains("public static partial class TypedPaths", srcText);
        Assert.Contains("public static class Src", srcText);
        Assert.Contains("public const string Template1 = \"src/Template1.anyext\";", srcText);
        Assert.Contains("public static class FolderA", srcText);
        Assert.Contains("public const string Template2 = \"src/folderA/Template2.anyext\";", srcText);
        Assert.Contains("public static class FolderB", srcText);
        Assert.Contains("public const string Template3 = \"src/folderB/Template3.anyext\";", srcText);
        Assert.Contains("public const string Template4 = \"src/folderB/Template4.anyext\";", srcText);

        var templateText = generatedFiles["TypedPaths.Template.g.cs"];
        Assert.Contains("public static partial class TypedPaths", templateText);
        Assert.Contains("public static class Template", templateText);
        Assert.Contains("public static class Email", templateText);
        Assert.Contains("public const string Welcome = \"template/email/welcome.txt\";", templateText);
        Assert.Contains("public static class Sms", templateText);
        Assert.Contains("public const string Otp = \"template/sms/otp.txt\";", templateText);
    }

    [Fact]
    public void SanitizesToPascalCaseAndValidIdentifiers()
    {
        var generatedText = RunAndGetGeneratedFiles([
            new TestAdditionalFile("./src/folder-name/my-template.anyext", "template"),
            new TestAdditionalFile("./src/folder-name/1-template.anyext", "template")
        ], [
            new TypedPathsFolderConfig("/src", "Src")
        ])["TypedPaths.Src.g.cs"];

        Assert.Contains("public static class FolderName", generatedText);
        Assert.Contains("public const string MyTemplate = \"src/folder-name/my-template.anyext\";", generatedText);
        Assert.Contains("public const string _1Template = \"src/folder-name/1-template.anyext\";", generatedText);
    }

    [Fact]
    public void AddsDeterministicSuffixWhenIdentifiersCollide()
    {
        var generatedText = RunAndGetGeneratedFiles([
            new TestAdditionalFile("./src/my-file.anyext", "template"),
            new TestAdditionalFile("./src/my_file.anyext", "template")
        ], [
            new TypedPathsFolderConfig("/src", "Src")
        ])["TypedPaths.Src.g.cs"];

        Assert.Contains("public const string MyFile = \"src/my-file.anyext\";", generatedText);
        Assert.Contains("public const string MyFile_2 = \"src/my_file.anyext\";", generatedText);
    }

    [Fact]
    public void NormalizesSeparatorsAndIncludesEachTopLevelRoot()
    {
        var generatedFiles = RunAndGetGeneratedFiles([
            new TestAdditionalFile(@".\src\folderA\Template2.anyext", "template"),
            new TestAdditionalFile(@".\other\Ignored.anyext", "template")
        ], [
            new TypedPathsFolderConfig("/src", "Src"),
            new TypedPathsFolderConfig("/other", "Other")
        ]);

        Assert.Contains("public const string Template2 = \"src/folderA/Template2.anyext\";", generatedFiles["TypedPaths.Src.g.cs"]);
        Assert.Contains("public static class Other", generatedFiles["TypedPaths.Other.g.cs"]);
        Assert.Contains("public const string Ignored = \"other/Ignored.anyext\";", generatedFiles["TypedPaths.Other.g.cs"]);
    }

    [Fact]
    public void EmitsOnePartialFilePerTopLevelFolder()
    {
        var generatedFiles = RunAndGetGeneratedFiles([
            new TestAdditionalFile("./src/Page.cshtml", "template"),
            new TestAdditionalFile("./template/email/welcome.txt", "template")
        ], [
            new TypedPathsFolderConfig("/src", "Src"),
            new TypedPathsFolderConfig("/template")
        ]);

        Assert.Equal(2, generatedFiles.Count);
        Assert.Contains("TypedPaths.Src.g.cs", generatedFiles.Keys);
        Assert.Contains("TypedPaths.Template.g.cs", generatedFiles.Keys);
        Assert.Contains("public static partial class TypedPaths", generatedFiles["TypedPaths.Src.g.cs"]);
        Assert.Contains("public static partial class TypedPaths", generatedFiles["TypedPaths.Template.g.cs"]);
    }

    [Fact]
    public void GeneratesNothingWhenNoTypedPathsFolderConfigured()
    {
        var generatedFiles = RunAndGetGeneratedFiles([
            new TestAdditionalFile("./Page.cshtml", "template"),
            new TestAdditionalFile("./welcome.txt", "template")
        ], []);

        Assert.Empty(generatedFiles);
    }

    [Fact]
    public void GeneratesPathsWhenMetadataKeysAreLowerCase()
    {
        var generator = new SourceGeneratorWithAdditionalFiles();
        var additionalFiles = new AdditionalText[]
        {
            new TestAdditionalFile("./src/Template1.anyext", "template")
        };

        var driver = CSharpGeneratorDriver.Create(generator)
            .AddAdditionalTexts([.. additionalFiles])
            .WithUpdatedAnalyzerConfigOptions(new TestAnalyzerConfigOptionsProvider(
                [new TypedPathsFolderConfig("/src", "Src")],
                useLowerCaseMetadataKeys: true));

        var compilation = CSharpCompilation.Create(nameof(GeneratesPathsWhenMetadataKeysAreLowerCase));
        var runResult = driver.RunGenerators(compilation, TestContext.Current.CancellationToken).GetRunResult();
        var generatedPaths = runResult.GeneratedTrees
            .Select(tree => Path.GetFileName(tree.FilePath))
            .ToArray();

        Assert.Contains("TypedPaths.Src.g.cs", generatedPaths);
    }

    [Fact]
    public void InfersRootFoldersWhenMetadataMissing()
    {
        var generator = new SourceGeneratorWithAdditionalFiles();
        var additionalFiles = new AdditionalText[]
        {
            new TestAdditionalFile("./src/Template1.anyext", "template"),
            new TestAdditionalFile("./template/email/welcome.txt", "template")
        };

        var driver = CSharpGeneratorDriver.Create(generator)
            .AddAdditionalTexts([.. additionalFiles])
            .WithUpdatedAnalyzerConfigOptions(new TestAnalyzerConfigOptionsProvider([]));

        var compilation = CSharpCompilation.Create(nameof(InfersRootFoldersWhenMetadataMissing));
        var runResult = driver.RunGenerators(compilation, TestContext.Current.CancellationToken).GetRunResult();
        var generatedFiles = runResult.GeneratedTrees
            .ToDictionary(
                static t => Path.GetFileName(t.FilePath),
                static t => t.GetText().ToString(),
                StringComparer.Ordinal);

        Assert.Contains("TypedPaths.Src.g.cs", generatedFiles.Keys);
        Assert.Contains("TypedPaths.Template.g.cs", generatedFiles.Keys);
    }

    private static IReadOnlyDictionary<string, string> RunAndGetGeneratedFiles(
        IEnumerable<AdditionalText> additionalFiles,
        IEnumerable<TypedPathsFolderConfig> folders)
    {
        var generator = new SourceGeneratorWithAdditionalFiles();

        var driver = CSharpGeneratorDriver.Create(generator)
            .AddAdditionalTexts([.. additionalFiles])
            .WithUpdatedAnalyzerConfigOptions(new TestAnalyzerConfigOptionsProvider(folders));

        var compilation = CSharpCompilation.Create(nameof(SourceGeneratorWithAdditionalFilesTests));
        var runResult = driver.RunGenerators(compilation).GetRunResult();
        var byFilePath = runResult.GeneratedTrees
            .ToDictionary(
                static t => Path.GetFileName(t.FilePath),
                static t => t.GetText().ToString(),
                StringComparer.Ordinal);

        return byFilePath;
    }

    private readonly record struct TypedPathsFolderConfig(string Include, string? ClassName = null);

    private sealed class TestAnalyzerConfigOptionsProvider(
        IEnumerable<TypedPathsFolderConfig> folders,
        bool useLowerCaseMetadataKeys = false) : AnalyzerConfigOptionsProvider
    {
        private readonly TypedPathsFolderConfig[] _folders = [.. folders];
        private readonly bool _useLowerCaseMetadataKeys = useLowerCaseMetadataKeys;

        public override AnalyzerConfigOptions GetOptions(SyntaxTree tree) => EmptyAnalyzerConfigOptions.Instance;

        public override AnalyzerConfigOptions GetOptions(AdditionalText textFile)
        {
            var fileSegments = GetPathSegments(textFile.Path);
            var match = _folders
                .Select(folder => new
                {
                    Folder = folder,
                    Segments = GetPathSegments(folder.Include)
                })
                .Where(x => x.Segments.Count > 0 && IsPrefix(fileSegments, x.Segments))
                .OrderByDescending(x => x.Segments.Count)
                .FirstOrDefault();

            if (match is null)
            {
                return EmptyAnalyzerConfigOptions.Instance;
            }

            var includeKey = _useLowerCaseMetadataKeys ? FolderIncludeMetadataKeyLower : FolderIncludeMetadataKey;
            var classNameKey = _useLowerCaseMetadataKeys ? FolderClassNameMetadataKeyLower : FolderClassNameMetadataKey;
            return new DictionaryAnalyzerConfigOptions(new Dictionary<string, string>(StringComparer.Ordinal)
            {
                [includeKey] = match.Folder.Include,
                [classNameKey] = match.Folder.ClassName ?? string.Empty
            });
        }

        public override AnalyzerConfigOptions GlobalOptions { get; } = EmptyAnalyzerConfigOptions.Instance;

        private static IReadOnlyList<string> GetPathSegments(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return [];
            }

            var normalized = path.Replace('\\', '/').Trim().Trim('/').TrimStart('.');
            return string.IsNullOrWhiteSpace(normalized)
                ? []
                : normalized.Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        }

        private static bool IsPrefix(IReadOnlyList<string> source, IReadOnlyList<string> prefix)
        {
            if (prefix.Count == 0 || source.Count < prefix.Count)
            {
                return false;
            }

            for (var index = 0; index < prefix.Count; index++)
            {
                if (!string.Equals(source[index], prefix[index], StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }
            }

            return true;
        }
    }

    private sealed class DictionaryAnalyzerConfigOptions(IReadOnlyDictionary<string, string> values)
        : AnalyzerConfigOptions
    {
        public override bool TryGetValue(string key, out string value) => values.TryGetValue(key, out value!);
    }

    private sealed class EmptyAnalyzerConfigOptions : AnalyzerConfigOptions
    {
        public static readonly EmptyAnalyzerConfigOptions Instance = new();

        public override bool TryGetValue(string key, out string value)
        {
            value = string.Empty;
            return false;
        }
    }
}