using Build.Results;
using Build.Targets.Package.Models;
using Build.Shared.Manifest;
using Cake.Core.IO;

namespace Build.Validation.Packaging;

/// <summary>
/// Post-pack validator (G57) — asserts the README mapping block currently matches the
/// manifest-driven generator output. Normalizes line endings for a stable diff.
/// Returns <see langword="null"/> when the README block is current, or a single failure
/// <see cref="ValidationCheck"/> describing the staleness.
/// </summary>
public sealed class ReadmeMappingTableValidator(IFileSystem fileSystem) : IReadmeMappingTableValidator
{
    private readonly IFileSystem _fileSystem = fileSystem ?? throw new ArgumentNullException(nameof(fileSystem));

    public ValidationCheck? Validate(
        PackageFamilyConfig family,
        FilePath readmePath,
        ManifestConfig manifestConfig)
    {
        ArgumentNullException.ThrowIfNull(family);
        ArgumentNullException.ThrowIfNull(readmePath);
        ArgumentNullException.ThrowIfNull(manifestConfig);

        var file = _fileSystem.GetFile(readmePath);
        if (!file.Exists)
        {
            return Failure($"G57: README file '{readmePath.FullPath}' does not exist.");
        }

        string readmeContent;
        using (var stream = file.OpenRead())
        using (var reader = new StreamReader(stream))
        {
            readmeContent = reader.ReadToEnd();
        }

        var expectedBlock = ReadmeMappingTableBlock.BuildBlock(manifestConfig);
        if (!ReadmeMappingTableBlock.TryExtractBlock(readmeContent, out var actualBlock))
        {
            return Failure(
                $"G57: README '{readmePath.GetFilename().FullPath}' is missing mapping table markers '{ReadmeMappingTableBlock.StartMarker}' and/or '{ReadmeMappingTableBlock.EndMarker}'.");
        }

        var lineEnding = readmeContent.Contains("\r\n", StringComparison.Ordinal) ? "\r\n" : "\n";
        var normalizedExpected = ReadmeMappingTableBlock.NormalizeLineEndings(expectedBlock, lineEnding);
        var normalizedActual = ReadmeMappingTableBlock.NormalizeLineEndings(actualBlock, lineEnding);

        if (string.Equals(normalizedExpected, normalizedActual, StringComparison.Ordinal))
        {
            return null;
        }

        return Failure(
            $"G57: README mapping table block in '{readmePath.GetFilename().FullPath}' is stale and does not match manifest-driven generator output.");
    }

    private static ValidationCheck Failure(string message)
        => new("README mapping table", ValidationSeverity.Error, message, Code: "G57");
}
