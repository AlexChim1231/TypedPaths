using System.Collections.Immutable;
using System.Text;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;

namespace TypedPaths.Generator;

[Generator]
public class SourceGeneratorWithAdditionalFiles : IIncrementalGenerator
{
    private const string ProjectDirPropertyKey = "build_property.ProjectDir";
    private const string MsBuildProjectDirectoryPropertyKey = "build_property.MSBuildProjectDirectory";
    private const string FolderIncludeMetadataKey = "build_metadata.AdditionalFiles.TypedPathsFolderInclude";
    private const string FolderClassNameMetadataKey = "build_metadata.AdditionalFiles.TypedPathsClassName";
    private const string FolderIncludeMetadataKeyLower = "build_metadata.additionalfiles.TypedPathsFolderInclude";
    private const string FolderClassNameMetadataKeyLower = "build_metadata.additionalfiles.TypedPathsClassName";
    private const string FolderIncludeMetadataKeyAllLower = "build_metadata.additionalfiles.typedpathsfolderinclude";
    private const string FolderClassNameMetadataKeyAllLower = "build_metadata.additionalfiles.typedpathsclassname";

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var additionalFilesProvider = context.AdditionalTextsProvider
            .Combine(context.AnalyzerConfigOptionsProvider)
            .Select((pair, _) =>
            {
                var additionalFile = pair.Left;
                var options = pair.Right.GetOptions(additionalFile);
                var includePath = TryGetMetadata(options, FolderIncludeMetadataKey, FolderIncludeMetadataKeyLower, FolderIncludeMetadataKeyAllLower);
                var className = TryGetMetadata(options, FolderClassNameMetadataKey, FolderClassNameMetadataKeyLower, FolderClassNameMetadataKeyAllLower);
                return new AdditionalFileInput(additionalFile.Path, includePath, className);
            })
            .Collect();
        var generationOptionsProvider = context.AnalyzerConfigOptionsProvider
            .Select((options, _) => CreateGenerationOptions(options.GlobalOptions));

        context.RegisterSourceOutput(
            additionalFilesProvider.Combine(generationOptionsProvider),
            (ctx, input) => GenerateCode(ctx, input.Left, input.Right));
    }

    private static void GenerateCode(SourceProductionContext context, ImmutableArray<AdditionalFileInput> fileInputs, GenerationOptions options)
    {
        if (fileInputs.IsDefaultOrEmpty)
        {
            return;
        }

        var folderStates = new Dictionary<string, FolderState>(StringComparer.OrdinalIgnoreCase);
        foreach (var fileInput in fileInputs)
        {
            var relativePath = TryGetRelativePath(fileInput.Path, options.ProjectDirectory);
            if (relativePath is null)
            {
                continue;
            }

            var includePath = fileInput.FolderInclude;
            var className = fileInput.FolderClassName;
            if (string.IsNullOrWhiteSpace(includePath))
            {
                var inferred = TryInferFolderConfiguration(relativePath);
                if (inferred is not null)
                {
                    includePath = inferred.Value.IncludePath;
                    className = inferred.Value.ClassName;
                }
            }

            if (string.IsNullOrWhiteSpace(includePath))
            {
                continue;
            }

            var includeSegments = GetPathSegments(includePath);
            if (includeSegments.Count == 0)
            {
                continue;
            }

            var relativeSegments = GetPathSegments(relativePath);
            if (!IsPrefix(relativeSegments, includeSegments) || relativeSegments.Count <= includeSegments.Count)
            {
                continue;
            }

            var rootName = string.IsNullOrWhiteSpace(className)
                ? includeSegments[^1]
                : className!;
            var folderKey = $"{string.Join("/", includeSegments)}|{rootName}";
            if (!folderStates.TryGetValue(folderKey, out var folderState))
            {
                folderState = new FolderState(new TypedPathsFolder(includeSegments, rootName));
                folderStates.Add(folderKey, folderState);
            }

            AddFilePath(folderState.Root, relativeSegments, includeSegments.Count, relativePath);
        }

        if (folderStates.Count == 0 || folderStates.Values.All(state => !state.Root.HasContent))
        {
            return;
        }

        var usedRootMembers = new HashSet<string>(StringComparer.Ordinal);
        foreach (var state in folderStates.Values.Where(static state => state.Root.HasContent))
        {
            var root = state.Root;
            root.GeneratedName = ToUniqueIdentifier(ToPascalIdentifier(root.Name), usedRootMembers);
            var source = BuildSource(root);
            context.AddSource($"TypedPaths.{root.GeneratedName}.g.cs", SourceText.From(source, Encoding.UTF8));
        }
    }

    private static GenerationOptions CreateGenerationOptions(AnalyzerConfigOptions globalOptions)
    {
        var projectDirectory = TryGetProjectDirectory(globalOptions);
        return new GenerationOptions(projectDirectory);
    }

    private static string? TryGetMetadata(AnalyzerConfigOptions options, params string[] keys)
    {
        foreach (var key in keys)
        {
            if (options.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value))
            {
                return value;
            }

            var lowerInvariantKey = key.ToLowerInvariant();
            if (!string.Equals(lowerInvariantKey, key, StringComparison.Ordinal)
                && options.TryGetValue(lowerInvariantKey, out value)
                && !string.IsNullOrWhiteSpace(value))
            {
                return value;
            }
        }

        return null;
    }

    private static string? TryGetProjectDirectory(AnalyzerConfigOptions globalOptions)
    {
        if (globalOptions.TryGetValue(ProjectDirPropertyKey, out var projectDirectory)
            && !string.IsNullOrWhiteSpace(projectDirectory))
        {
            return projectDirectory;
        }

        if (globalOptions.TryGetValue(MsBuildProjectDirectoryPropertyKey, out projectDirectory)
            && !string.IsNullOrWhiteSpace(projectDirectory))
        {
            return projectDirectory;
        }

        return null;
    }

    /// <summary>
    /// Returns path segments (directory and file names) from root to leaf using System.IO.Path.
    /// Input is normalized so both forward and back slashes are handled.
    /// </summary>
    private static List<string> GetPathSegments(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return [];
        }

        // Normalize both \ and / to forward slash so paths like .\src\folder work on Linux
        var normalized = path.Replace('\\', Path.AltDirectorySeparatorChar)
            .Replace(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            .Trim()
            .Trim(Path.AltDirectorySeparatorChar, '.');

        if (normalized.Length > 0 && normalized[0] == Path.AltDirectorySeparatorChar)
        {
            normalized = normalized.TrimStart(Path.AltDirectorySeparatorChar);
        }

        if (string.IsNullOrWhiteSpace(normalized))
        {
            return [];
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

    private static string? TryGetRelativePath(string path, string? projectDirectory)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        if (!string.IsNullOrWhiteSpace(projectDirectory) && Path.IsPathRooted(path))
        {
            var fullPath = Path.GetFullPath(path);
            var projectDir = Path.GetFullPath(projectDirectory);
            var relative = Path.GetRelativePath(projectDir, fullPath)
                .Replace('\\', Path.AltDirectorySeparatorChar)
                .Replace(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                .Trim()
                .Trim(Path.AltDirectorySeparatorChar);
            if (!string.IsNullOrWhiteSpace(relative) && !relative.StartsWith("..", StringComparison.Ordinal))
            {
                return relative;
            }
        }

        if (Path.IsPathRooted(path))
        {
            return null;
        }

        var normalized = path.Replace('\\', Path.AltDirectorySeparatorChar)
            .Replace(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            .Trim()
            .TrimStart('.')
            .Trim(Path.AltDirectorySeparatorChar);

        return string.IsNullOrWhiteSpace(normalized) ? null : normalized;
    }

    private static (string IncludePath, string ClassName)? TryInferFolderConfiguration(string relativePath)
    {
        var segments = GetPathSegments(relativePath);
        if (segments.Count < 2)
        {
            return null;
        }

        var topLevelFolder = segments[0];
        return ($"/{topLevelFolder}", topLevelFolder);
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

    private static void AddFilePath(DirectoryNode root, IReadOnlyList<string> segments, int includeSegmentsCount, string relativePath)
    {
        var current = root;
        for (var i = includeSegmentsCount; i < segments.Count - 1; i++)
        {
            var segment = segments[i];
            if (!current.Directories.TryGetValue(segment, out var next))
            {
                next = new DirectoryNode(segment);
                current.Directories.Add(segment, next);
            }

            current = next;
        }

        var fileName = segments[^1];
        current.Files.Add(new FileNode(fileName, relativePath));
    }

    private static string BuildSource(DirectoryNode root)
    {
        var builder = new StringBuilder();
        builder.AppendLine("// <auto-generated/>");
        builder.AppendLine();
        builder.AppendLine("namespace TypedPaths;");
        builder.AppendLine();
        builder.AppendLine("public static partial class TypedPaths");
        builder.AppendLine("{");
        AppendDirectoryWithName(builder, root, 1, new HashSet<string>(StringComparer.Ordinal), preserveName: true);

        builder.AppendLine("}");
        return builder.ToString();
    }

    private static void AppendDirectoryWithName(
        StringBuilder builder,
        DirectoryNode directory,
        int indentLevel,
        HashSet<string> usedMembers,
        bool preserveName = false)
    {
        directory.GeneratedName = preserveName
            ? ToPascalIdentifier(directory.GeneratedName)
            : ToUniqueIdentifier(ToPascalIdentifier(directory.Name), usedMembers);

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


    private static string EscapeString(string value) => value.Replace("\\", @"\\").Replace("\"", "\\\"");

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

    private sealed class DirectoryNode(string name)
    {
        public string Name { get; } = name;

        public string GeneratedName { get; set; } = string.Empty;

        public Dictionary<string, DirectoryNode> Directories { get; } = new(StringComparer.OrdinalIgnoreCase);

        public List<FileNode> Files { get; } = [];

        public bool HasContent => Directories.Count > 0 || Files.Count > 0;
    }

    private sealed class FileNode(string name, string relativePath)
    {
        public string Name { get; } = name;

        public string RelativePath { get; } = relativePath;
    }

    private sealed record TypedPathsFolder(IReadOnlyList<string> IncludeSegments, string ClassName);

    private sealed class FolderState(TypedPathsFolder configuration)
    {
        public TypedPathsFolder Configuration { get; } = configuration;

        public DirectoryNode Root { get; } = new(configuration.ClassName);
    }

    private readonly record struct AdditionalFileInput(string Path, string? FolderInclude, string? FolderClassName);

    private readonly record struct GenerationOptions(string? ProjectDirectory);
}