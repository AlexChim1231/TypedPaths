using System.Text;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

using TypedPaths.Generator.Utils;

namespace TypedPaths.Generator;

[Generator]
public class Generator : IIncrementalGenerator
{
    private const string TypedPathsMetadataKey = "build_metadata.AdditionalFiles.TypedPaths";
    private const string TypedPathsRootMetadataKey = "build_metadata.AdditionalFiles.TypedPathsRoot";
    private const string TypedPathsClassNameMetadataKey = "build_metadata.AdditionalFiles.TypedPathsClassName";
    private const string TypedPathsProjectDirMetadataKey = "build_metadata.AdditionalFiles.TypedPathsProjectDir";

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var collection = context.AdditionalTextsProvider
            .Combine(context.AnalyzerConfigOptionsProvider)
            .Select(static (pair, _) =>
            {
                var file = pair.Left;
                var config = pair.Right.GetOptions(file);

                if (!config.TryGetValue(TypedPathsRootMetadataKey, out var rootPath) || string.IsNullOrWhiteSpace(rootPath))
                {
                    if (!config.TryGetValue(TypedPathsMetadataKey, out rootPath) || string.IsNullOrWhiteSpace(rootPath))
                    {
                        return null;
                    }
                }

                config.TryGetValue(TypedPathsClassNameMetadataKey, out var className);
                config.TryGetValue(TypedPathsProjectDirMetadataKey, out var projectDirectory);

                var rootFullPath = Path.GetFullPath(rootPath);
                var projectDirFullPath = string.IsNullOrWhiteSpace(projectDirectory)
                    ? Path.GetDirectoryName(file.Path) ?? rootFullPath
                    : Path.GetFullPath(projectDirectory);

                return new FileData(file, rootFullPath, className, projectDirFullPath);
            })
            .Where(static x => x != null)
            .Collect();

        context.RegisterSourceOutput(collection, static (spc, files) =>
        {
            if (files.IsDefaultOrEmpty) return;

            var groups = files
                .Where(f => f != null)
                .Select(f => f!)
                .GroupBy(f => (f.RootPath, f.ProjectDirectory));

            foreach (var group in groups)
            {
                var rootPath = group.Key.RootPath;
                var projectDirectory = group.Key.ProjectDirectory;
                var rootRelativePath = ToProjectRelativePath(projectDirectory, rootPath);
                var className = ResolveRootClassName(group, rootPath);

                var tree = BuildTree(group, rootRelativePath);

                var code = SourceHelper.GenerateRoot(className, tree);

                spc.AddSource($"TypedPaths.{className}.g.cs", SourceText.From(code, Encoding.UTF8));
            }
        });
    }

    private static PathNode BuildTree(IEnumerable<FileData> files, string rootRelativePath)
    {
        var root = new PathNode
        {
            Name = "Root",
            FullPath = rootRelativePath,
            IsFile = false
        };

        foreach (var file in files.OrderBy(f => f.File.Path, StringComparer.OrdinalIgnoreCase))
        {
            var relativePath = Path.GetRelativePath(file.RootPath, file.File.Path).Replace('\\', '/');
            if (relativePath.StartsWith("..", StringComparison.Ordinal))
            {
                continue;
            }

            var parts = relativePath
                .Split('/', StringSplitOptions.RemoveEmptyEntries);

            var currentNode = root;

            for (int i = 0; i < parts.Length; i++)
            {
                string part = parts[i];
                bool isFile = i == parts.Length - 1;
                string safeName = isFile
                    ? ResolveFileNodeName(currentNode, part)
                    : ResolveFolderNodeName(currentNode, part);

                if (!currentNode.Children.TryGetValue(safeName, out var nextNode))
                {
                    string cumulativeSuffix = string.Join("/", parts.Take(i + 1));
                    string cumulativePath = string.IsNullOrWhiteSpace(root.FullPath)
                        ? cumulativeSuffix
                        : $"{root.FullPath}/{cumulativeSuffix}";

                    nextNode = new PathNode
                    {
                        Name = safeName,
                        FullPath = cumulativePath,
                        IsFile = isFile
                    };
                    currentNode.Children[safeName] = nextNode;
                }

                currentNode = nextNode;
            }
        }

        return root;
    }

    private static string ResolveRootClassName(IEnumerable<FileData> files, string rootPath)
    {
        var explicitName = files
            .Select(f => f.ClassName)
            .FirstOrDefault(name => !string.IsNullOrWhiteSpace(name));

        if (!string.IsNullOrWhiteSpace(explicitName))
        {
            return GetSafeIdentifier(explicitName);
        }

        var folderName = Path.GetFileName(rootPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
        return GetSafeIdentifier(folderName);
    }

    private static string ResolveFolderNodeName(PathNode parent, string folderPart)
    {
        var baseName = GetSafeIdentifier(folderPart);
        if (parent.Children.TryGetValue(baseName, out var existing) && existing.IsFile)
        {
            var existingFilePart = Path.GetFileName(existing.FullPath);
            var renamedFile = EnsureUniqueName(parent.Children, BuildConflictingFileName(existingFilePart, baseName), baseName);
            parent.Children.Remove(baseName);
            existing.Name = renamedFile;
            parent.Children[renamedFile] = existing;
        }

        return baseName;
    }

    private static string ResolveFileNodeName(PathNode parent, string filePart)
    {
        var baseName = GetSafeIdentifier(filePart);
        return !parent.Children.TryGetValue(baseName, out _)
            ? baseName
            : EnsureUniqueName(parent.Children, BuildConflictingFileName(filePart, baseName), null);
    }

    private static string BuildConflictingFileName(string filePart, string baseName)
    {
        var extension = Path.GetExtension(filePart).TrimStart('.');
        var suffix = string.IsNullOrWhiteSpace(extension) ? "File" : GetSafeIdentifier(extension);
        if (string.IsNullOrWhiteSpace(suffix))
        {
            suffix = "File";
        }

        return baseName + suffix;
    }

    private static string EnsureUniqueName(
        IReadOnlyDictionary<string, PathNode> siblings,
        string candidate,
        string? allowExistingKey)
    {
        if (string.IsNullOrWhiteSpace(candidate))
        {
            candidate = "_";
        }

        if (!siblings.ContainsKey(candidate) || string.Equals(candidate, allowExistingKey, StringComparison.Ordinal))
        {
            return candidate;
        }

        int index = 2;
        while (siblings.ContainsKey($"{candidate}_{index}"))
        {
            index++;
        }

        return $"{candidate}_{index}";
    }

    private static string GetSafeIdentifier(string value)
    {
        var candidate = PascalConverter.GetSafeClassName(value);
        return string.IsNullOrWhiteSpace(candidate) ? "_" : candidate;
    }

    private static string ToProjectRelativePath(string projectDirectory, string rootPath)
    {
        var relativePath = Path.GetRelativePath(projectDirectory, rootPath).Replace('\\', '/');
        return relativePath == "." ? string.Empty : relativePath;
    }
}