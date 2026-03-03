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

    private readonly IReadOnlyDictionary<string, string> _additionalFileToRootPath;
    private readonly AnalyzerConfigOptions _globalOptions;

    public TestAnalyzerConfigOptionsProvider(IReadOnlyDictionary<string, string> additionalFileToRootPath)
    {
        _additionalFileToRootPath = additionalFileToRootPath;
        _globalOptions = new TestAnalyzerConfigOptions();
    }

    public override AnalyzerConfigOptions GlobalOptions => _globalOptions;

    public override AnalyzerConfigOptions GetOptions(AdditionalText additionalText)
    {
        if (additionalText == null)
            return _globalOptions;

        if (_additionalFileToRootPath.TryGetValue(additionalText.Path, out var rootPath))
        {
            return new TestAnalyzerConfigOptions(new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                [TypedPathsMetadataKey] = rootPath
            });
        }

        return _globalOptions;
    }

    public override AnalyzerConfigOptions GetOptions(SyntaxTree syntaxTree) => _globalOptions;
}
