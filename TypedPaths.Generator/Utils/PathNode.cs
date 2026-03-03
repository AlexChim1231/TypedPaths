namespace TypedPaths.Generator.Utils;

public class PathNode
{
    public string Name { get; set; }
    public string FullPath { get; set; }
    public bool IsFile { get; set; }
    public Dictionary<string, PathNode> Children { get; } = [];
}