using System.Collections.Generic;

using Microsoft.CodeAnalysis.Diagnostics;

namespace TypedPaths.Generator.Tests.Utils;

/// <summary>
/// Test double for <see cref="AnalyzerConfigOptions"/> that returns config from a dictionary.
/// </summary>
public sealed class TestAnalyzerConfigOptions : AnalyzerConfigOptions
{
    private readonly IReadOnlyDictionary<string, string> _values;

    public TestAnalyzerConfigOptions(IReadOnlyDictionary<string, string>? values = null)
    {
        _values = values ?? new Dictionary<string, string>();
    }

    public override bool TryGetValue(string key, out string? value)
    {
        if (_values.TryGetValue(key, out var v))
        {
            value = v;
            return true;
        }
        value = null;
        return false;
    }

    public override IEnumerable<string> Keys => _values.Keys;
}
