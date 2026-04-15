#pragma warning disable MA0045

using System.Text.Json;
using Build.Modules.Contracts;
using Build.Modules.Coverage.Models;
using Cake.Core.IO;

namespace Build.Modules.Coverage;

/// <summary>
/// Reads <c>build/coverage-baseline.json</c> — the ratchet floor committed to the repo.
/// <see cref="CoverageBaseline"/> carries its own <c>[JsonPropertyName]</c> mapping and
/// <c>required</c> guard, so this reader is a thin deserialization + error-wrapping layer.
/// </summary>
/// <remarks>
/// File access goes through Cake's <see cref="IFileSystem"/> abstraction for mockability
/// and consistency with the rest of the build host.
/// </remarks>
public sealed class CoverageBaselineReader(IFileSystem fileSystem) : ICoverageBaselineReader
{
    private readonly IFileSystem _fileSystem = fileSystem ?? throw new ArgumentNullException(nameof(fileSystem));

    public CoverageBaseline Parse(string jsonContent)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(jsonContent);

        try
        {
            var baseline = JsonSerializer.Deserialize<CoverageBaseline>(jsonContent);
            return baseline
                ?? throw new ArgumentException("Coverage baseline JSON deserialized to null.", nameof(jsonContent));
        }
        catch (JsonException ex)
        {
            throw new ArgumentException($"Invalid coverage baseline JSON: {ex.Message}", nameof(jsonContent), ex);
        }
    }

    public CoverageBaseline ParseFile(FilePath path)
    {
        ArgumentNullException.ThrowIfNull(path);

        var file = _fileSystem.GetFile(path);
        using var stream = file.OpenRead();
        using var reader = new StreamReader(stream);
        var json = reader.ReadToEnd();

        return Parse(json);
    }
}
