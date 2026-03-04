using System;
using System.IO;

using TypedPaths;

// Sample usage of generated typed paths.
// Build the project first so the source generator emits TypedPaths.*.g.cs files.
// This project opts in roots via <TypedPathsFolder /> (src/ and template/).
// The targets file maps these folders to AdditionalFiles for generation.
//
// Generated path constants are project-relative ("before build"): they refer to the
// source layout (directory containing the .csproj), not the build output. Resolve them
// against the project root to get full file paths when running from bin/Debug or bin/Release.

var projectRoot = FindProjectRoot(AppContext.BaseDirectory);
if (projectRoot is null)
{
    Console.WriteLine("Could not find project root (run from build output so the .csproj can be located).");
    return 1;
}

Console.WriteLine("Typed path constants (relative paths):");
Console.WriteLine("  TypedPaths.Src.Template1.Value           = " + Src.Template1.Value);
Console.WriteLine("  TypedPaths.Src.FolderA.Template2.Value   = " + Src.FolderA.Template2.Value);
Console.WriteLine("  TypedPaths.Src.FolderB.Template3.Value   = " + Src.FolderB.Template3.Value);
Console.WriteLine("  TypedPaths.Src.FolderB.Template4.Value   = " + Src.FolderB.Template4.Value);
Console.WriteLine("  TypedPaths.Template.Email.Welcome.Value  = " + Template.Email.Welcome.Value);
Console.WriteLine("  TypedPaths.Template.Sms.Otp.Value        = " + Template.Sms.Otp.Value);

// Example: resolve project-relative path to full path (project root = before-build layout)
var fullPath = Path.GetFullPath(Path.Combine(projectRoot, Src.FolderA.Template2.Value));
Console.WriteLine();
Console.WriteLine("Example: resolve to full path (project-relative -> absolute):");
Console.WriteLine("  Path.Combine(projectRoot, TypedPaths.Src.FolderA.Template2.Value)");
Console.WriteLine("  = " + fullPath);
Console.WriteLine("  Exists: " + File.Exists(fullPath));

return 0;

/// <summary>
/// Locates the project directory (contains the .csproj). This is the "before build" root
/// that generated TypedPaths constants are relative to.
/// </summary>
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