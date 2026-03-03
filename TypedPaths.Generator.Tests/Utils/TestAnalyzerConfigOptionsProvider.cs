using System;
using System.Collections.Generic;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace TypedPaths.Generator.Tests.Utils;

/// <summary>
/// Test double for <see cref="AnalyzerConfigOptionsProvider"/> that maps each additional file path
/// to a root path for the TypedPaths metadata key.
/// </summary>
public sealed class TestAnalyzerConfigOptionsProvider : AnalyzerConfigOptionsProvider
{
    private const string TypedPathsMetadataKey = "build_metadata.AdditionalFiles.TypedPaths";
    private const string TypedPathsRootMetadataKey = "build_metadata.AdditionalFiles.TypedPathsRoot";
    private const string TypedPathsClassNameMetadataKey = "build_metadata.AdditionalFiles.TypedPathsClassName";
    private const string TypedPathsProjectDirMetadataKey = "build_metadata.AdditionalFiles.TypedPathsProjectDir";

    private readonly IReadOnlyDictionary<string, FileMetadata> _additionalFileMetadata;
    private readonly AnalyzerConfigOptions _globalOptions;

    public TestAnalyzerConfigOptionsProvider(IReadOnlyDictionary<string, string> additionalFileToRootPath)
        : this(BuildMetadata(additionalFileToRootPath))
    {
    }

    public TestAnalyzerConfigOptionsProvider(IReadOnlyDictionary<string, FileMetadata> additionalFileMetadata)
    {
        _additionalFileMetadata = additionalFileMetadata;
        _globalOptions = new TestAnalyzerConfigOptions();
    }

    public override AnalyzerConfigOptions GlobalOptions => _globalOptions;

    public override AnalyzerConfigOptions GetOptions(AdditionalText additionalText)
    {
        if (additionalText == null)
            return _globalOptions;

        if (_additionalFileMetadata.TryGetValue(additionalText.Path, out var metadata))
        {
            var options = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                [TypedPathsMetadataKey] = metadata.RootPath,
                [TypedPathsRootMetadataKey] = metadata.RootPath
            };

            if (!string.IsNullOrWhiteSpace(metadata.ClassName))
            {
                options[TypedPathsClassNameMetadataKey] = metadata.ClassName!;
            }

            if (!string.IsNullOrWhiteSpace(metadata.ProjectDirectory))
            {
                options[TypedPathsProjectDirMetadataKey] = metadata.ProjectDirectory!;
            }

            return new TestAnalyzerConfigOptions(options);
        }

        return _globalOptions;
    }

    public override AnalyzerConfigOptions GetOptions(SyntaxTree syntaxTree) => _globalOptions;

    private static IReadOnlyDictionary<string, FileMetadata> BuildMetadata(IReadOnlyDictionary<string, string> additionalFileToRootPath)
    {
        var map = new Dictionary<string, FileMetadata>(additionalFileToRootPath.Count, StringComparer.OrdinalIgnoreCase);
        foreach (var pair in additionalFileToRootPath)
        {
            map[pair.Key] = new FileMetadata(pair.Value);
        }

        return map;
    }

    public sealed record FileMetadata(string RootPath, string? ClassName = null, string? ProjectDirectory = null);
}
