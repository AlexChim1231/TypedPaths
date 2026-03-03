using Microsoft.CodeAnalysis;

namespace TypedPaths.Generator.Utils;

public sealed record FileData(
    AdditionalText File,
    string RootPath,
    string? ClassName,
    string ProjectDirectory
);