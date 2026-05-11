using System.Diagnostics.CodeAnalysis;
using Build.Data.Manifest.Models;
using Build.Validation.Conventions;
using NuGet.Versioning;

namespace Build.Targets.Package.Models;

/// <summary>
/// Human-readable half of the cross-referenced metadata pair. Builds, extracts, and upserts the version-mapping block delimited
/// by <see cref="StartMarker"/> / <see cref="EndMarker"/>. Asserted current by post-pack guardrail G57.
/// </summary>
public static class ReadmeMappingTableBlock
{
    public const string StartMarker = "<!-- JANSET:MAPPING-TABLE-START -->";
    public const string EndMarker = "<!-- JANSET:MAPPING-TABLE-END -->";

    public static string BuildBlock(ManifestConfig manifestConfig)
    {
        ArgumentNullException.ThrowIfNull(manifestConfig);

        var rows = new List<string>
        {
            StartMarker,
            "| Family | Version | Upstream | vcpkg Port |",
            "| --- | --- | --- | --- |",
        };

        foreach (var family in manifestConfig.PackageFamilies)
        {
            var library = manifestConfig.LibraryManifests.SingleOrDefault(
                candidate => string.Equals(candidate.Name, family.LibraryRef, StringComparison.OrdinalIgnoreCase));

            if (library is null)
            {
                continue;
            }

            if (!NuGetVersion.TryParse(library.VcpkgVersion, out var upstreamVersion))
            {
                throw new InvalidOperationException(
                    $"Cannot build README mapping table because manifest library '{library.Name}' has invalid vcpkg_version '{library.VcpkgVersion}'.");
            }

            var derivedFamilyVersion = $"{upstreamVersion.Major}.{upstreamVersion.Minor}.0";
            var managedPackageId = FamilyIdentifierConventions.ManagedPackageId(family.Name);
            var upstreamLabel = string.Equals(library.Name, "SDL2", StringComparison.OrdinalIgnoreCase)
                ? $"SDL {library.VcpkgVersion}"
                : $"{library.Name} {library.VcpkgVersion}";

            rows.Add($"| {managedPackageId} | {derivedFamilyVersion} | {upstreamLabel} | {library.VcpkgPortVersion} |");
        }

        rows.Add(EndMarker);

        return string.Join('\n', rows);
    }

    public static bool TryExtractBlock(string readmeContent, out string block)
    {
        ArgumentNullException.ThrowIfNull(readmeContent);

        var startIndex = readmeContent.IndexOf(StartMarker, StringComparison.Ordinal);
        var endIndex = readmeContent.IndexOf(EndMarker, StringComparison.Ordinal);

        if (startIndex < 0 || endIndex < 0 || endIndex < startIndex)
        {
            block = string.Empty;
            return false;
        }

        var length = (endIndex + EndMarker.Length) - startIndex;
        block = readmeContent.Substring(startIndex, length);
        return true;
    }

    public static string UpsertBlock(string readmeContent, string block)
    {
        ArgumentNullException.ThrowIfNull(readmeContent);
        ArgumentException.ThrowIfNullOrWhiteSpace(block);

        var lineEnding = DetectLineEnding(readmeContent);
        var normalizedBlock = NormalizeLineEndings(block, lineEnding);

        if (TryExtractBlock(readmeContent, out var existingBlock))
        {
            return readmeContent.Replace(existingBlock, normalizedBlock, StringComparison.Ordinal);
        }

        var suffix = readmeContent.EndsWith(lineEnding, StringComparison.Ordinal)
            ? string.Empty
            : lineEnding;

        return string.Concat(
            readmeContent,
            suffix,
            lineEnding,
            "## Version Mapping",
            lineEnding,
            lineEnding,
            normalizedBlock,
            lineEnding);
    }

    public static string NormalizeLineEndings(string text, string lineEnding)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentException.ThrowIfNullOrEmpty(lineEnding);

        var normalized = text
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n');

        return string.Equals(lineEnding, "\n", StringComparison.Ordinal)
            ? normalized
            : normalized.Replace("\n", lineEnding, StringComparison.Ordinal);
    }

    [SuppressMessage("Performance", "MA0089:Use an overload with char instead of string",
        Justification = "CRLF detection requires a two-character token.")]
    private static string DetectLineEnding(string text)
    {
        if (text.Contains("\r\n", StringComparison.Ordinal))
        {
            return "\r\n";
        }

        return "\n";
    }
}
