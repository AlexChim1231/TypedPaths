using System.Text.RegularExpressions;

namespace TypedPaths.Generator.Utils;

public static class PascalConverter
{
    private static readonly Regex StartingDigitRegex = new(@"^\d+", RegexOptions.Compiled);
    private static readonly Regex NonAlphanumericRegex = new(@"[^a-zA-Z0-9]+", RegexOptions.Compiled);

    public static string GetSafeClassName(string input)
    {
        string ext = Path.GetExtension(input).TrimStart('.');
        string fileName = Path.GetFileNameWithoutExtension(input);

        string baseName = string.IsNullOrWhiteSpace(fileName) ? ext : fileName;

        return ToPascalCase(baseName);
    }

    private static string ToPascalCase(string input)
    {
        var cleanInput = StartingDigitRegex.Replace(input, "");

        var parts = NonAlphanumericRegex.Split(cleanInput)
            .Where(s => !string.IsNullOrEmpty(s))
            .Select(static part => char.ToUpperInvariant(part[0]) + part.Substring(1));

        return string.Concat(parts);
    }
}