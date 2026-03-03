using System.Text.RegularExpressions;

namespace TypedPaths.Generator.Utils;

public static partial class PascalConverter
{
    public static string GetSafeClassName(string input)
    {
        string ext = Path.GetExtension(input).TrimStart('.');
        string fileName = Path.GetFileNameWithoutExtension(input);

        string baseName = string.IsNullOrWhiteSpace(fileName) ? ext : fileName;

        return ToPascalCase(baseName);
    }
    
    private static string ToPascalCase(string input)
    {
        var cleanInput = StartingDigitRegex().Replace(input, "");
        
        var parts = AlphanumericRegex().Split(cleanInput)
            .Where(s => !string.IsNullOrEmpty(s))
            .Select(static part => char.ToUpperInvariant(part[0]) + part[1..]);

        return string.Concat(parts);
    }

    [GeneratedRegex(@"^\d+")]
    private static partial Regex StartingDigitRegex();

    [GeneratedRegex(@"[^a-zA-Z0-9]+")]
    private static partial Regex AlphanumericRegex();
}