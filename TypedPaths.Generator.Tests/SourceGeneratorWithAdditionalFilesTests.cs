using System.Collections.Generic;
using System.Linq;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;

using TypedPaths.Generator.Tests.Utils;

using Xunit;

namespace TypedPaths.Generator.Tests;

public class SourceGeneratorWithAdditionalFilesTests
{
    [Fact]
    public void GeneratesNestedTypedPathsFromSrcTree()
    {
        var generatedText = RunAndGetTypedPathsCode([
            new TestAdditionalFile("./src/Template1.anyext", "template"),
            new TestAdditionalFile("./src/folderA/Template2.anyext", "template"),
            new TestAdditionalFile("./src/folderB/Template3.anyext", "template"),
            new TestAdditionalFile("./src/folderB/Template4.anyext", "template")
        ]);

        Assert.Contains("public static class TypedPaths", generatedText);
        Assert.Contains("public static class Src", generatedText);
        Assert.Contains("public const string Template1 = \"src/Template1.anyext\";", generatedText);
        Assert.Contains("public static class FolderA", generatedText);
        Assert.Contains("public const string Template2 = \"src/folderA/Template2.anyext\";", generatedText);
        Assert.Contains("public static class FolderB", generatedText);
        Assert.Contains("public const string Template3 = \"src/folderB/Template3.anyext\";", generatedText);
        Assert.Contains("public const string Template4 = \"src/folderB/Template4.anyext\";", generatedText);
    }

    [Fact]
    public void SanitizesToPascalCaseAndValidIdentifiers()
    {
        var generatedText = RunAndGetTypedPathsCode([
            new TestAdditionalFile("./src/folder-name/my-template.anyext", "template"),
            new TestAdditionalFile("./src/folder-name/1-template.anyext", "template")
        ]);

        Assert.Contains("public static class FolderName", generatedText);
        Assert.Contains("public const string MyTemplate = \"src/folder-name/my-template.anyext\";", generatedText);
        Assert.Contains("public const string _1Template = \"src/folder-name/1-template.anyext\";", generatedText);
    }

    [Fact]
    public void AddsDeterministicSuffixWhenIdentifiersCollide()
    {
        var generatedText = RunAndGetTypedPathsCode([
            new TestAdditionalFile("./src/my-file.anyext", "template"),
            new TestAdditionalFile("./src/my_file.anyext", "template")
        ]);

        Assert.Contains("public const string MyFile = \"src/my-file.anyext\";", generatedText);
        Assert.Contains("public const string MyFile_2 = \"src/my_file.anyext\";", generatedText);
    }

    [Fact]
    public void NormalizesSeparatorsAndIgnoresFilesOutsideSrc()
    {
        var generatedText = RunAndGetTypedPathsCode([
            new TestAdditionalFile(@".\src\folderA\Template2.anyext", "template"),
            new TestAdditionalFile(@".\other\Ignored.anyext", "template")
        ]);

        Assert.Contains("public const string Template2 = \"src/folderA/Template2.anyext\";", generatedText);
        Assert.DoesNotContain("Ignored", generatedText);
    }

    [Fact]
    public void UsesConfiguredBasePathInsteadOfHardcodedSrc()
    {
        var generatedText = RunAndGetTypedPathsCode(
            [
                new TestAdditionalFile("./templates/email/welcome.txt", "template"),
                new TestAdditionalFile("./templates/sms/otp.txt", "template"),
                new TestAdditionalFile("./src/should-not-exist.txt", "template")
            ],
            basePath: "/templates");

        Assert.Contains("public static class Templates", generatedText);
        Assert.Contains("public static class Email", generatedText);
        Assert.Contains("public const string Welcome = \"templates/email/welcome.txt\";", generatedText);
        Assert.Contains("public static class Sms", generatedText);
        Assert.Contains("public const string Otp = \"templates/sms/otp.txt\";", generatedText);
        Assert.DoesNotContain("ShouldNotExist", generatedText);
        Assert.DoesNotContain("public static class Src", generatedText);
    }

    private static string RunAndGetTypedPathsCode(IEnumerable<AdditionalText> additionalFiles, string basePath = "/src")
    {
        var generator = new SourceGeneratorWithAdditionalFiles();

        var driver = CSharpGeneratorDriver.Create(generator)
            .AddAdditionalTexts([.. additionalFiles])
            .WithUpdatedAnalyzerConfigOptions(new TestAnalyzerConfigOptionsProvider(basePath));

        var compilation = CSharpCompilation.Create(nameof(SourceGeneratorWithAdditionalFilesTests));
        var runResult = driver.RunGenerators(compilation).GetRunResult();
        var generatedTree = runResult.GeneratedTrees.Single(t => t.FilePath.EndsWith("TypedPaths.g.cs"));

        return generatedTree.GetText().ToString();
    }

    private sealed class TestAnalyzerConfigOptionsProvider(string basePath) : AnalyzerConfigOptionsProvider
    {
        public override AnalyzerConfigOptions GetOptions(SyntaxTree tree) => EmptyAnalyzerConfigOptions.Instance;

        public override AnalyzerConfigOptions GetOptions(AdditionalText textFile) => EmptyAnalyzerConfigOptions.Instance;

        public override AnalyzerConfigOptions GlobalOptions { get; } = new DictionaryAnalyzerConfigOptions(new Dictionary<string, string>
        {
            ["build_property.TypedPathsBasePath"] = basePath
        });
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