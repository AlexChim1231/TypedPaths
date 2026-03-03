using System.Collections.Immutable;
using System.Linq;
using System.Text;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

using TypedPaths.Generator.Utils;

namespace TypedPaths.Generator;

[Generator]
public class Generator : IIncrementalGenerator
{
    private const string TypedPathsMetadataKey = "build_metadata.AdditionalFiles.TypedPaths";

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var collection = context.AdditionalTextsProvider
            .Combine(context.AnalyzerConfigOptionsProvider)
            .Select(static (pair, _) =>
            {
                var file = pair.Left;
                var config = pair.Right.GetOptions(file);

                if (!config.TryGetValue(TypedPathsMetadataKey, out var path) || string.IsNullOrWhiteSpace(path))
                {
                    return null;
                }

                return new FileData(file, path);
            })
            .Where(static x => x != null)
            .Collect();

        context.RegisterSourceOutput(collection, static (spc, files) =>
        {
            if (files.IsDefaultOrEmpty) return;
            
            var groups = files
                .Where(f => f != null)
                .Select(f => f!)
                .GroupBy(f => f.Path);

            foreach (var group in groups)
            {
                var path = group.Key;
                
                var trimmedPath = path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                var folderName = Path.GetFileName(trimmedPath);
                var className = PascalConverter.GetSafeClassName(folderName);
                
                var tree = BuildTree(group);
                
                var code = SourceHelper.GenerateRoot(className, tree);
                
                spc.AddSource($"TypedPath.{className}.g.cs", SourceText.From(code, Encoding.UTF8));
            }
        });
    }

    private static PathNode BuildTree(IEnumerable<FileData> files)
    {
        var root = new PathNode
        {
            Name = "Root"
        };

        foreach (var file in files)
        {
            var relativePath = Path.GetRelativePath(file.Path, file.File.Path).Replace('\\', '/');
            
            var parts = relativePath.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            
            var currentNode = root;

            for (int i = 0; i < parts.Length; i++)
            {
                string part = parts[i];
                bool isFile = (i == parts.Length - 1);
            
                string safeName = PascalConverter.GetSafeClassName(part);

                if (isFile && currentNode.Children.ContainsKey(safeName))
                {
                    string ext = Path.GetExtension(part).TrimStart('.');
                    safeName += PascalConverter.GetSafeClassName(ext);
                }

                if (!currentNode.Children.TryGetValue(safeName, out var nextNode))
                {
                    string cumulativePath = string.Join("/", parts.Take(i + 1));

                    nextNode = new PathNode
                    {
                        Name = safeName,
                        FullPath = cumulativePath
                    };
                    currentNode.Children[safeName] = nextNode;
                }
            
                currentNode = nextNode;
            }
        }

        return root;
    }
}