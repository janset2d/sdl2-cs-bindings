using System.Text.Json;
using Build.Context;
using Build.Domain.Coverage.Models;
using Cake.Core.IO;

namespace Build.Infrastructure.Coverage;

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

        return DeserializeBaseline(
            () => CakeExtensions.DeserializeJson<CoverageBaseline>(jsonContent),
            nameof(jsonContent));
    }

    public CoverageBaseline ParseFile(FilePath path)
    {
        ArgumentNullException.ThrowIfNull(path);

        var file = _fileSystem.GetFile(path);
        using var stream = file.OpenRead();
        using var buffer = new MemoryStream();
        stream.CopyTo(buffer);

        return DeserializeBaseline(
            () => CakeExtensions.DeserializeJson<CoverageBaseline>(buffer.ToArray()),
            nameof(path));
    }

    private static CoverageBaseline DeserializeBaseline(Func<CoverageBaseline?> deserialize, string parameterName)
    {
        try
        {
            var baseline = deserialize();
            return baseline
                ?? throw new ArgumentException("Coverage baseline JSON deserialized to null.", parameterName);
        }
        catch (JsonException ex)
        {
            throw new ArgumentException($"Invalid coverage baseline JSON: {ex.Message}", parameterName, ex);
        }
    }
}
