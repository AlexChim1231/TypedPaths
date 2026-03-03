namespace TypedPaths.Generator.Utils;

public class PathNode
{
    public string Name { get; set; } = null!;
    public string FullPath { get; init; } = null!;
    public bool IsFile { get; init; }
    public Dictionary<string, PathNode> Children { get; } = [];
}