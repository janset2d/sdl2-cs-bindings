using System.Diagnostics.CodeAnalysis;
using Build.Context;
using Build.Context.Models;
using Build.Domain.Packaging.Models;
using Build.Domain.Paths;
using Build.Domain.Preflight;
using Cake.Common.IO;
using Cake.Core;
using Cake.Core.IO;
using NuGet.Versioning;

namespace Build.Domain.Packaging;

/// <summary>
/// Human-readable half of the cross-referenced metadata pair (see <see cref="NativePackageMetadata"/>
/// for the machine-readable half). Builds, extracts, and upserts the version-mapping block delimited
/// by <see cref="StartMarker"/> / <see cref="EndMarker"/>. Asserted current by post-pack guardrail G57.
/// </summary>
public static class ReadmeMappingTable
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

/// <summary>
/// Generator (G57) — upserts the README mapping table block during packing. Runs once per
/// packaging invocation (not per family) because the block reflects the manifest state, not
/// a single family's state.
/// </summary>
public sealed class ReadmeMappingTableGenerator(
    ManifestConfig manifestConfig,
    IPathService pathService,
    ICakeContext cakeContext) : IReadmeMappingTableGenerator
{
    private readonly ManifestConfig _manifestConfig = manifestConfig ?? throw new ArgumentNullException(nameof(manifestConfig));
    private readonly IPathService _pathService = pathService ?? throw new ArgumentNullException(nameof(pathService));
    private readonly ICakeContext _cakeContext = cakeContext ?? throw new ArgumentNullException(nameof(cakeContext));

    public async Task UpdateAsync(CancellationToken cancellationToken = default)
    {
        var readmePath = _pathService.GetReadmeFile();

        if (!_cakeContext.FileExists(readmePath))
        {
            throw new InvalidOperationException($"README mapping table generation failed: file '{readmePath.FullPath}' does not exist.");
        }

        var expectedBlock = ReadmeMappingTable.BuildBlock(_manifestConfig);
        var original = await _cakeContext.ReadAllTextAsync(readmePath);
        cancellationToken.ThrowIfCancellationRequested();

        var updated = ReadmeMappingTable.UpsertBlock(original, expectedBlock);

        if (string.Equals(original, updated, StringComparison.Ordinal))
        {
            return;
        }

        await _cakeContext.WriteAllTextAsync(readmePath, updated);
        cancellationToken.ThrowIfCancellationRequested();
    }
}

/// <summary>
/// Post-pack validator (G57) — asserts the README mapping block currently matches the
/// manifest-driven generator output. Normalizes line endings for a stable diff.
/// </summary>
public sealed class ReadmeMappingTableValidator(IFileSystem fileSystem)
{
    private readonly IFileSystem _fileSystem = fileSystem ?? throw new ArgumentNullException(nameof(fileSystem));

    public PackageValidationCheck Validate(
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
            return BuildFailure(
                family,
                readmePath,
                "README mapping block present",
                "README missing",
                $"G57: README file '{readmePath.FullPath}' does not exist.");
        }

        string readmeContent;
        using (var stream = file.OpenRead())
        using (var reader = new StreamReader(stream))
        {
            readmeContent = reader.ReadToEnd();
        }

        var expectedBlock = ReadmeMappingTable.BuildBlock(manifestConfig);
        if (!ReadmeMappingTable.TryExtractBlock(readmeContent, out var actualBlock))
        {
            return BuildFailure(
                family,
                readmePath,
                ReadmeMappingTable.StartMarker + " ... " + ReadmeMappingTable.EndMarker,
                "<missing markers>",
                $"G57: README '{readmePath.GetFilename().FullPath}' is missing mapping table markers '{ReadmeMappingTable.StartMarker}' and/or '{ReadmeMappingTable.EndMarker}'.");
        }

        var lineEnding = readmeContent.Contains("\r\n", StringComparison.Ordinal) ? "\r\n" : "\n";
        var normalizedExpected = ReadmeMappingTable.NormalizeLineEndings(expectedBlock, lineEnding);
        var normalizedActual = ReadmeMappingTable.NormalizeLineEndings(actualBlock, lineEnding);

        if (string.Equals(normalizedExpected, normalizedActual, StringComparison.Ordinal))
        {
            return new PackageValidationCheck(
                FamilyIdentifier: family.Name,
                PackagePath: readmePath,
                Kind: PackageValidationCheckKind.ReadmeMappingTableCurrent,
                IsValid: true,
                ExpectedValue: "manifest-aligned mapping block",
                ActualValue: "current",
                ErrorMessage: null);
        }

        return BuildFailure(
            family,
            readmePath,
            normalizedExpected,
            normalizedActual,
            $"G57: README mapping table block in '{readmePath.GetFilename().FullPath}' is stale and does not match manifest-driven generator output.");
    }

    private static PackageValidationCheck BuildFailure(
        PackageFamilyConfig family,
        FilePath readmePath,
        string expected,
        string actual,
        string message)
    {
        return new PackageValidationCheck(
            FamilyIdentifier: family.Name,
            PackagePath: readmePath,
            Kind: PackageValidationCheckKind.ReadmeMappingTableCurrent,
            IsValid: false,
            ExpectedValue: expected,
            ActualValue: actual,
            ErrorMessage: message);
    }
}
