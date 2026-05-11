using System.Globalization;
using Build.Data.Manifest;
using Build.Data.Manifest.Models;
using Build.Data.Versions;
using Build.Host;
using Cake.Core;
using Cake.Frosting;
using NuGet.Versioning;

namespace Build.Targets.ResolveVersionsFromManifest;

[TaskName("ResolveVersionsFromManifest")]
[TaskDescription("Resolves per-family versions from manifest upstream + --suffix; writes versions.json to --versions-file path")]
public sealed class ResolveVersionsFromManifestTask(
    IManifestRepository manifestRepository,
    IVersionFileRepository versionFileRepository) : AsyncFrostingTask<BuildContext>
{
    private readonly IManifestRepository _manifestRepository = manifestRepository ?? throw new ArgumentNullException(nameof(manifestRepository));
    private readonly IVersionFileRepository _versionFileRepository = versionFileRepository ?? throw new ArgumentNullException(nameof(versionFileRepository));

    public override async Task RunAsync(BuildContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (context.VersionsFilePath is null)
        {
            throw new CakeException(
                "ResolveVersionsFromManifest requires --versions-file <path>. " +
                "Example: --versions-file artifacts/resolve-versions/versions.json");
        }

        if (string.IsNullOrWhiteSpace(context.ResolveVersionsSuffix))
        {
            throw new CakeException(
                "ResolveVersionsFromManifest requires --suffix. Example: " +
                "--suffix=ci.$GITHUB_RUN_ID or --suffix=local.$(date -u +%Y%m%dT%H%M%SZ).");
        }

        var manifest = _manifestRepository.Load();
        var suffix = context.ResolveVersionsSuffix.Trim();
        var scope = BuildScope(context.ResolveVersionsScope);
        var families = ResolveFamiliesInScope(manifest, scope);

        var versions = new PackageFamilyVersionSet(
            families.Select(family => new PackageFamilyVersion(new PackageFamilyId(family.Name), BuildVersionFor(manifest, family, suffix))));

        await _versionFileRepository.SaveAsync(context.VersionsFilePath, versions);
    }

    private static HashSet<string> BuildScope(IReadOnlyList<string> rawScope)
    {
        var scope = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var entry in rawScope)
        {
            if (!string.IsNullOrWhiteSpace(entry))
            {
                scope.Add(entry.Trim());
            }
        }

        return scope;
    }

    private static List<PackageFamilyConfig> ResolveFamiliesInScope(ManifestConfig manifest, HashSet<string> requestedScope)
    {
        if (requestedScope.Count == 0)
        {
            if (manifest.PackageFamilies.Count == 0)
            {
                throw new CakeException(
                    "ResolveVersionsFromManifest cannot resolve versions: manifest.json package_families[] is empty. " +
                    "Declare at least one family before invoking this target.");
            }

            return [.. manifest.PackageFamilies];
        }

        var resolved = new List<PackageFamilyConfig>(requestedScope.Count);
        var missing = new List<string>();

        foreach (var requested in requestedScope)
        {
            var match = manifest.PackageFamilies.SingleOrDefault(candidate =>
                string.Equals(candidate.Name, requested, StringComparison.OrdinalIgnoreCase));

            if (match is null)
            {
                missing.Add(requested);
            }
            else
            {
                resolved.Add(match);
            }
        }

        if (missing.Count > 0)
        {
            var missingList = string.Join(", ", missing.Order(StringComparer.OrdinalIgnoreCase));
            throw new CakeException(
                $"ResolveVersionsFromManifest cannot resolve versions for requested scope entries not in manifest.json package_families[]: {missingList}. " +
                "Add the families to manifest or narrow the --scope filter.");
        }

        return resolved;
    }

    private static NuGetVersion BuildVersionFor(ManifestConfig manifest, PackageFamilyConfig family, string suffix)
    {
        var library = manifest.LibraryManifests.SingleOrDefault(candidate =>
            string.Equals(candidate.Name, family.LibraryRef, StringComparison.OrdinalIgnoreCase));

        if (library is null)
        {
            throw new CakeException(
                $"ResolveVersionsFromManifest cannot resolve version for family '{family.Name}' because " +
                $"library_ref '{family.LibraryRef}' does not exist in manifest library_manifests[].");
        }

        if (!NuGetVersion.TryParse(library.VcpkgVersion, out var upstreamVersion))
        {
            throw new CakeException(
                $"ResolveVersionsFromManifest cannot resolve version for family '{family.Name}' because " +
                $"library '{library.Name}' has non-semantic vcpkg_version '{library.VcpkgVersion}'.");
        }

        var candidate = string.Create(CultureInfo.InvariantCulture, $"{upstreamVersion.Major}.{upstreamVersion.Minor}.0-{suffix}");
        if (!NuGetVersion.TryParse(candidate, out var composed))
        {
            throw new CakeException(
                $"ResolveVersionsFromManifest produced an invalid NuGet SemVer for family '{family.Name}': " +
                $"'{candidate}'. Suffix '{suffix}' likely contains characters disallowed in a prerelease " +
                "identifier (allowed: ASCII alphanumerics, hyphens, dot-separated segments).");
        }

        return composed;
    }
}
