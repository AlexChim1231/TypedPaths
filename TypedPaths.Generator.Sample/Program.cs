using System;
using System.IO;

using TypedPathsRoot = TypedPaths.TypedPaths;

// Sample usage of generated typed paths.
// Build the project first so the source generator emits TypedPaths.*.g.cs files.
// This project opts in roots via <TypedPathsFolder /> (src/ and template/).
// The targets file maps these folders to AdditionalFiles/Content for generation.

var projectRoot = FindProjectRoot(AppContext.BaseDirectory);
if (projectRoot is null)
{
    Console.WriteLine("Could not find project root.");
    return 1;
}

Console.WriteLine("Typed path constants (relative paths):");
Console.WriteLine("  TypedPaths.Src.Template1.Value           = " + TypedPathsRoot.Src.Template1.Value);
Console.WriteLine("  TypedPaths.Src.FolderA.Template2.Value   = " + TypedPathsRoot.Src.FolderA.Template2.Value);
Console.WriteLine("  TypedPaths.Src.FolderB.Template3.Value   = " + TypedPathsRoot.Src.FolderB.Template3.Value);
Console.WriteLine("  TypedPaths.Src.FolderB.Template4.Value   = " + TypedPathsRoot.Src.FolderB.Template4.Value);
Console.WriteLine("  TypedPaths.Template.Email.Welcome.Value  = " + TypedPathsRoot.Template.Email.Welcome.Value);
Console.WriteLine("  TypedPaths.Template.Sms.Otp.Value        = " + TypedPathsRoot.Template.Sms.Otp.Value);

// Example: resolve to full path and check if file exists
var fullPath = Path.Combine(projectRoot, TypedPathsRoot.Src.FolderA.Template2.Value);
Console.WriteLine();
Console.WriteLine("Example: resolve to full path:");
Console.WriteLine("  Path.Combine(projectRoot, TypedPaths.Src.FolderA.Template2.Value)");
Console.WriteLine("  = " + fullPath);
Console.WriteLine("  Exists: " + File.Exists(fullPath));

return 0;

static string? FindProjectRoot(string startDir)
{
    var dir = startDir;
    while (!string.IsNullOrEmpty(dir))
    {
        if (File.Exists(Path.Combine(dir, "TypedPaths.Generator.Sample.csproj")))
            return dir;
        dir = Path.GetDirectoryName(dir);
    }
    return null;
}