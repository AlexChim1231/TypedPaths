using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Text;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;

namespace TypedPaths.Generator;

[Generator]
public class SourceGeneratorWithAdditionalFiles : IIncrementalGenerator
{
    private const string BasePathPropertyKey = "build_property.TypedPathsBasePath";

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var additionalPathsProvider = context.AdditionalTextsProvider
            .Select((file, _) => file.Path)
            .Collect();
        var configuredBasePathProvider = context.AnalyzerConfigOptionsProvider
            .Select((options, _) => NormalizeConfiguredBasePath(options.GlobalOptions));

        context.RegisterSourceOutput(
            additionalPathsProvider.Combine(configuredBasePathProvider),
            (ctx, input) => GenerateCode(ctx, input.Left, input.Right));
    }

    private static void GenerateCode(SourceProductionContext context, ImmutableArray<string> filePaths, string configuredBasePath)
    {
        if (filePaths.IsDefaultOrEmpty)
        {
            return;
        }

        var basePathSegments = GetPathSegments(configuredBasePath);
        var relativePaths = filePaths
            .Select(path => TryGetRelativePath(path, basePathSegments))
            .Where(path => path is not null)
            .Select(path => path!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (relativePaths.Length == 0)
        {
            return;
        }

        var root = new DirectoryNode(string.Empty);
        foreach (var relativePath in relativePaths)
        {
            AddFilePath(root, relativePath);
        }

        var source = BuildSource(root);
        context.AddSource("TypedPaths.g.cs", SourceText.From(source, Encoding.UTF8));
    }

    private static string NormalizeConfiguredBasePath(AnalyzerConfigOptions globalOptions)
    {
        if (!globalOptions.TryGetValue(BasePathPropertyKey, out var configured))
        {
            return "src";
        }

        var segments = GetPathSegments(configured);
        if (segments.Count == 0)
        {
            return "src";
        }

        return string.Join(Path.AltDirectorySeparatorChar.ToString(), segments);
    }

    /// <summary>
    /// Returns path segments (directory and file names) from root to leaf using System.IO.Path.
    /// Input is normalized so both forward and back slashes are handled.
    /// </summary>
    private static IReadOnlyList<string> GetPathSegments(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return Array.Empty<string>();
        }

        var normalized = path.Replace(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            .Trim()
            .Trim(Path.AltDirectorySeparatorChar, '.');
        if (normalized.Length > 0 && normalized[0] == Path.AltDirectorySeparatorChar)
        {
            normalized = normalized.TrimStart(Path.AltDirectorySeparatorChar);
        }

        if (string.IsNullOrWhiteSpace(normalized))
        {
            return Array.Empty<string>();
        }

        var segments = new List<string>();
        var current = normalized;
        while (!string.IsNullOrEmpty(current))
        {
            var name = Path.GetFileName(current);
            if (!string.IsNullOrEmpty(name))
            {
                segments.Add(name);
            }

            var dir = Path.GetDirectoryName(current);
            if (string.IsNullOrEmpty(dir) || dir == current)
            {
                break;
            }

            current = dir;
        }

        segments.Reverse();
        return segments;
    }

    private static string? TryGetRelativePath(string path, IReadOnlyList<string> basePathSegments)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        var segments = GetPathSegments(path);
        if (segments.Count == 0)
        {
            return null;
        }

        var basePathStartIndex = FindSubsequenceStart(segments, basePathSegments);
        if (basePathStartIndex < 0 || basePathStartIndex + basePathSegments.Count >= segments.Count)
        {
            return null;
        }

        var relativeSegments = segments.Skip(basePathStartIndex).ToArray();
        return string.Join(Path.AltDirectorySeparatorChar.ToString(), relativeSegments);
    }

    private static int FindSubsequenceStart(IReadOnlyList<string> source, IReadOnlyList<string> pattern)
    {
        if (source.Count == 0 || pattern.Count == 0 || pattern.Count > source.Count)
        {
            return -1;
        }

        for (var start = 0; start <= source.Count - pattern.Count; start++)
        {
            var allSegmentsMatch = !pattern.Where((t, offset) => !string.Equals(source[start + offset], t, StringComparison.OrdinalIgnoreCase)).Any();

            if (allSegmentsMatch)
            {
                return start;
            }
        }

        return -1;
    }

    private static void AddFilePath(DirectoryNode root, string relativePath)
    {
        var segments = GetPathSegments(relativePath);
        if (segments.Count == 0)
        {
            return;
        }

        var current = root;
        for (var i = 0; i < segments.Count - 1; i++)
        {
            var segment = segments[i];
            if (!current.Directories.TryGetValue(segment, out var next))
            {
                next = new DirectoryNode(segment);
                current.Directories.Add(segment, next);
            }

            current = next;
        }

        var fileName = segments[segments.Count - 1];
        current.Files.Add(new FileNode(fileName, relativePath));
    }

    private static string BuildSource(DirectoryNode srcRoot)
    {
        var builder = new StringBuilder();
        builder.AppendLine("// <auto-generated/>");
        builder.AppendLine();
        builder.AppendLine("namespace TypedPaths;");
        builder.AppendLine();
        builder.AppendLine("public static class TypedPaths");
        builder.AppendLine("{");

        var usedMembers = new HashSet<string>(StringComparer.Ordinal);
        foreach (var childDirectory in srcRoot.Directories.Values.OrderBy(node => node.Name, StringComparer.OrdinalIgnoreCase))
        {
            AppendDirectoryWithName(builder, childDirectory, 1, usedMembers);
        }

        var childIndent = new string(' ', 4);
        foreach (var file in srcRoot.Files.OrderBy(file => file.RelativePath, StringComparer.OrdinalIgnoreCase))
        {
            var baseName = ToPascalIdentifier(Path.GetFileNameWithoutExtension(file.Name));
            var memberName = ToUniqueIdentifier(baseName, usedMembers);
            builder.AppendLine($"{childIndent}public const string {memberName} = \"{EscapeString(file.RelativePath)}\";");
        }

        builder.AppendLine("}");
        return builder.ToString();
    }

    private static void AppendDirectoryWithName(StringBuilder builder, DirectoryNode directory, int indentLevel, HashSet<string> usedMembers)
    {
        directory.GeneratedName = ToUniqueIdentifier(ToPascalIdentifier(directory.Name), usedMembers);

        var indent = new string(' ', indentLevel * 4);
        builder.AppendLine($"{indent}public static class {directory.GeneratedName}");
        builder.AppendLine($"{indent}{{");

        var childIndent = new string(' ', (indentLevel + 1) * 4);
        var usedChildMembers = new HashSet<string>(StringComparer.Ordinal);

        foreach (var childDirectory in directory.Directories.Values.OrderBy(node => node.Name, StringComparer.OrdinalIgnoreCase))
        {
            AppendDirectoryWithName(builder, childDirectory, indentLevel + 1, usedChildMembers);
        }

        foreach (var file in directory.Files.OrderBy(file => file.RelativePath, StringComparer.OrdinalIgnoreCase))
        {
            var baseName = ToPascalIdentifier(Path.GetFileNameWithoutExtension(file.Name));
            var memberName = ToUniqueIdentifier(baseName, usedChildMembers);
            builder.AppendLine($"{childIndent}public const string {memberName} = \"{EscapeString(file.RelativePath)}\";");
        }

        builder.AppendLine($"{indent}}}");
    }


    private static string EscapeString(string value) => value.Replace("\\", "\\\\").Replace("\"", "\\\"");

    private static string ToPascalIdentifier(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "_";
        }

        var parts = new List<string>();
        var token = new StringBuilder();
        foreach (var ch in value)
        {
            if (char.IsLetterOrDigit(ch))
            {
                token.Append(ch);
            }
            else if (token.Length > 0)
            {
                parts.Add(token.ToString());
                token.Clear();
            }
        }

        if (token.Length > 0)
        {
            parts.Add(token.ToString());
        }

        if (parts.Count == 0)
        {
            return "_";
        }

        var result = string.Concat(parts.Select(Capitalize));
        if (!char.IsLetter(result[0]) && result[0] != '_')
        {
            result = $"_{result}";
        }

        return result;
    }

    private static string Capitalize(string token)
    {
        if (token.Length == 0)
        {
            return token;
        }

        return token.Length == 1
            ? char.ToUpperInvariant(token[0]).ToString()
            : $"{char.ToUpperInvariant(token[0])}{token.Substring(1)}";
    }

    private static string ToUniqueIdentifier(string baseName, HashSet<string> usedNames)
    {
        var candidate = string.IsNullOrWhiteSpace(baseName) ? "_" : baseName;
        var suffix = 1;
        while (!usedNames.Add(candidate))
        {
            suffix++;
            candidate = $"{baseName}_{suffix}";
        }

        return candidate;
    }

    private sealed class DirectoryNode
    {
        public DirectoryNode(string name)
        {
            Name = name;
        }

        public string Name { get; }

        public string GeneratedName { get; set; } = string.Empty;

        public Dictionary<string, DirectoryNode> Directories { get; } = new(StringComparer.OrdinalIgnoreCase);

        public List<FileNode> Files { get; } = [];
    }

    private sealed class FileNode(string name, string relativePath)
    {
        public string Name { get; } = name;

        public string RelativePath { get; } = relativePath;
    }
}