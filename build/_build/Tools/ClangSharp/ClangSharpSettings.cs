using Cake.Core.IO;

namespace Build.Tools.ClangSharp;

public sealed record ClangSharpSettings
{
    public required DirectoryPath WorkingDirectory { get; init; }
    public required IReadOnlyList<string> Config { get; init; }
    public required IReadOnlyList<FilePath> ResponseFiles { get; init; }
    public required string MethodClassName { get; init; }
    public required string LibraryPath { get; init; }
    public required string Namespace { get; init; }
    public required string AccessSpecifier { get; init; }
    public required IReadOnlyList<DirectoryPath> IncludeDirectories { get; init; }
    public required FilePath File { get; init; }
    public required FilePath Output { get; init; }
    public IReadOnlyList<string> Defines { get; init; } = [];
    public IReadOnlyList<string> Undefines { get; init; } = [];
    public IReadOnlyList<string> Exclusions { get; init; } = [];
}
