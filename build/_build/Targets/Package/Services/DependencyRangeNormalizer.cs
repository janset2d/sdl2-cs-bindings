using System.Diagnostics.CodeAnalysis;
using System.IO.Compression;
using System.Text;
using System.Xml.Linq;
using Build.Data.Manifest;
using Build.Validation.Conventions;
using Cake.Common.IO;
using Cake.Core;
using Cake.Core.Diagnostics;
using Cake.Core.IO;
using NuGet.Versioning;

namespace Build.Targets.Package.Services;

/// <summary>
/// G56 cross-family dependency range normalizer. Rewrites the managed nuspec inside
/// the packed managed .nupkg so every cross-family dependency declares the canonical
/// lower-and-upper bound range <c>[lowerBound, (UpstreamMajor + 1).0.0)</c>. Sealed
/// concrete; single consumer (<c>PackageFamilyPacker</c>); no test seam beyond unit
/// tests against <c>FakeFileSystem</c>.
/// </summary>
public sealed class DependencyRangeNormalizer(
    ICakeContext cakeContext,
    ICakeLog log,
    ManifestConfig manifestConfig)
{
    private readonly ICakeContext _cakeContext = cakeContext ?? throw new ArgumentNullException(nameof(cakeContext));
    private readonly ICakeLog _log = log ?? throw new ArgumentNullException(nameof(log));
    private readonly ManifestConfig _manifestConfig = manifestConfig ?? throw new ArgumentNullException(nameof(manifestConfig));

    [SuppressMessage("Design", "MA0051:Method is too long",
        Justification = "G56 normalization intentionally keeps zip read, nuspec parse, range rewrite, and zip update in one linear path for debuggability.")]
    public async Task NormalizeAsync(
        PackageFamilyConfig family,
        FilePath managedPackagePath,
        string lowerBoundVersion,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(family);
        ArgumentNullException.ThrowIfNull(managedPackagePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(lowerBoundVersion);

        if (family.DependsOn.Count == 0)
        {
            return;
        }

        if (!_cakeContext.FileExists(managedPackagePath))
        {
            _log.Verbose(
                "Skipping cross-family dependency normalization because managed package '{0}' does not exist yet.",
                managedPackagePath.GetFilename().FullPath);
            return;
        }

        await using var packageStream = _cakeContext.FileSystem
            .GetFile(managedPackagePath)
            .Open(FileMode.Open, FileAccess.ReadWrite, FileShare.None);

        using var archive = new ZipArchive(packageStream, ZipArchiveMode.Update, leaveOpen: false);

        var nuspecCandidates = archive.Entries.Where(entry =>
            entry.FullName.EndsWith(".nuspec", StringComparison.OrdinalIgnoreCase) &&
            !entry.FullName.StartsWith("package/", StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (nuspecCandidates.Count == 0)
        {
            _log.Warning(
                "Managed package '{0}' does not contain a nuspec entry. Skipping cross-family dependency normalization.",
                managedPackagePath.GetFilename().FullPath);
            return;
        }

        if (nuspecCandidates.Count > 1)
        {
            throw new CakeException(
                $"Managed package '{managedPackagePath.GetFilename().FullPath}' contains {nuspecCandidates.Count} nuspec entries " +
                $"({string.Join(", ", nuspecCandidates.Select(c => c.FullName))}). Cross-family dependency normalization expects exactly one — " +
                "this is likely a packaging-tool regression.");
        }

        var nuspecEntry = nuspecCandidates[0];

        string nuspecContent;
#pragma warning disable CA1849, S6966 // ZipArchiveEntry.Open sync used intentionally for small metadata reads
        using (var reader = new StreamReader(nuspecEntry.Open(), Encoding.UTF8, detectEncodingFromByteOrderMarks: true, leaveOpen: false))
#pragma warning restore CA1849, S6966
        {
            nuspecContent = await reader.ReadToEndAsync(ct);
        }

        var document = XDocument.Parse(nuspecContent, LoadOptions.PreserveWhitespace);
        var root = document.Root;
        if (root is null)
        {
            return;
        }

        var xmlNamespace = root.Name.Namespace;
        var dependencyElements = root
            .Descendants(xmlNamespace + "dependency")
            .ToList();

        var hasChanges = false;
        foreach (var dependencyFamily in family.DependsOn)
        {
            ct.ThrowIfCancellationRequested();

            var dependencyPackageId = FamilyIdentifierConventions.ManagedPackageId(dependencyFamily);
            var upperBound = ResolveCrossFamilyUpperBound(dependencyFamily);
            var expectedRange = $"[{lowerBoundVersion}, {upperBound})";

            foreach (var dependencyElement in dependencyElements.Where(element =>
                         string.Equals((string?)element.Attribute("id"), dependencyPackageId, StringComparison.OrdinalIgnoreCase)))
            {
                var currentVersion = (string?)dependencyElement.Attribute("version");
                if (string.Equals(currentVersion, expectedRange, StringComparison.Ordinal))
                {
                    continue;
                }

                dependencyElement.SetAttributeValue("version", expectedRange);
                hasChanges = true;
            }
        }

        if (!hasChanges)
        {
            return;
        }

        var nuspecEntryName = nuspecEntry.FullName;
        nuspecEntry.Delete();
        var updatedNuspecEntry = archive.CreateEntry(nuspecEntryName, CompressionLevel.Optimal);

        var updatedContent = document.Declaration is null
            ? document.ToString(SaveOptions.DisableFormatting)
            : string.Concat(document.Declaration, Environment.NewLine, document.ToString(SaveOptions.DisableFormatting));

#pragma warning disable CA1849, S6966 // ZipArchiveEntry.Open sync used intentionally for small metadata writes
        await using var updatedEntryStream = updatedNuspecEntry.Open();
#pragma warning restore CA1849, S6966
        await using var writer = new StreamWriter(updatedEntryStream, new UTF8Encoding(false));
        await writer.WriteAsync(updatedContent);

        _log.Verbose(
            "Normalized cross-family dependency ranges in managed package '{0}' for family '{1}'.",
            managedPackagePath.GetFilename().FullPath,
            family.Name);
    }

    private NuGetVersion ResolveCrossFamilyUpperBound(string dependencyFamily)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(dependencyFamily);

        var dependencyFamilyConfig = _manifestConfig.PackageFamilies.SingleOrDefault(candidate =>
            string.Equals(candidate.Name, dependencyFamily, StringComparison.OrdinalIgnoreCase));

        if (dependencyFamilyConfig is null)
        {
            throw new CakeException(
                $"Cannot resolve cross-family dependency range for '{dependencyFamily}' because it does not exist in manifest package_families[].");
        }

        var dependencyLibrary = _manifestConfig.LibraryManifests.SingleOrDefault(candidate =>
            string.Equals(candidate.Name, dependencyFamilyConfig.LibraryRef, StringComparison.OrdinalIgnoreCase));

        if (dependencyLibrary is null)
        {
            throw new CakeException(
                $"Cannot resolve cross-family dependency range for '{dependencyFamily}' because library_ref '{dependencyFamilyConfig.LibraryRef}' does not exist in manifest library_manifests[].");
        }

        if (!NuGetVersion.TryParse(dependencyLibrary.VcpkgVersion, out var upstreamVersion))
        {
            throw new CakeException(
                $"Cannot resolve cross-family dependency range for '{dependencyFamily}' because vcpkg_version '{dependencyLibrary.VcpkgVersion}' is invalid.");
        }

        return new NuGetVersion(upstreamVersion.Major + 1, 0, 0);
    }
}
