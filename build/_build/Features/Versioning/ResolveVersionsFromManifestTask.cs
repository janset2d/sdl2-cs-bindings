using System.Globalization;
using Build.Host;
using Build.Shared.Manifest;
using Cake.Core;
using Cake.Frosting;
using NuGet.Versioning;

namespace Build.Features.Versioning;

/// <summary>
/// Resolves per-family versions from <c>manifest.json library_manifests[].vcpkg_version</c>
/// upstream major/minor + caller-supplied <c>--suffix</c>, then writes
/// <c>artifacts/resolve-versions/versions.json</c>. Output shape:
/// <c>&lt;UpstreamMajor&gt;.&lt;UpstreamMinor&gt;.0-&lt;suffix&gt;</c> per family.
/// <para>
/// Inputs read from <see cref="BuildContext.ParsedArguments"/>: <c>Suffix</c> (required)
/// and <c>Scope</c> (optional, empty=all families). Self-validates at task entry per the
/// per-task validation principle.
/// </para>
/// </summary>
[TaskName("ResolveVersionsFromManifest")]
[TaskDescription("Resolves per-family versions from manifest upstream + --suffix; emits artifacts/resolve-versions/versions.json")]
public sealed class ResolveVersionsFromManifestTask(ManifestConfig manifestConfig, VersionsJsonWriter writer) : AsyncFrostingTask<BuildContext>
{
    private readonly ManifestConfig _manifestConfig = manifestConfig ?? throw new ArgumentNullException(nameof(manifestConfig));
    private readonly VersionsJsonWriter _writer = writer ?? throw new ArgumentNullException(nameof(writer));

    public override async Task RunAsync(BuildContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(context.ParsedArguments);

        if (string.IsNullOrWhiteSpace(context.ParsedArguments.Suffix))
        {
            throw new CakeException(
                "ResolveVersionsFromManifest requires --suffix. Example: " +
                "--suffix=ci.$GITHUB_RUN_ID or --suffix=local.$(date -u +%Y%m%dT%H%M%SZ).");
        }

        var suffix = context.ParsedArguments.Suffix.Trim();
        var scope = BuildScope(context.ParsedArguments.Scope);

        var families = ResolveFamiliesInScope(scope);

        var mapping = new Dictionary<string, NuGetVersion>(families.Count, StringComparer.OrdinalIgnoreCase);
        foreach (var family in families)
        {
            mapping[family.Name] = BuildVersionFor(family, suffix);
        }

        await _writer.WriteAsync(mapping);
    }

    private static HashSet<string> BuildScope(IList<string>? rawScope)
    {
        var scope = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (rawScope is null || rawScope.Count == 0)
        {
            return scope;
        }

        foreach (var entry in rawScope)
        {
            if (string.IsNullOrWhiteSpace(entry))
            {
                continue;
            }

            scope.Add(entry.Trim());
        }

        return scope;
    }

    private List<PackageFamilyConfig> ResolveFamiliesInScope(HashSet<string> requestedScope)
    {
        if (requestedScope.Count == 0)
        {
            if (_manifestConfig.PackageFamilies.Count == 0)
            {
                throw new CakeException(
                    "ResolveVersionsFromManifest cannot resolve versions: manifest.json package_families[] is empty. " +
                    "Declare at least one family before invoking this target.");
            }

            return [.. _manifestConfig.PackageFamilies];
        }

        var resolved = new List<PackageFamilyConfig>(requestedScope.Count);
        var missing = new List<string>();

        foreach (var requested in requestedScope)
        {
            var match = _manifestConfig.PackageFamilies.SingleOrDefault(candidate =>
                string.Equals(candidate.Name, requested, StringComparison.OrdinalIgnoreCase));

            if (match is null)
            {
                missing.Add(requested);
                continue;
            }

            resolved.Add(match);
        }

        if (missing.Count > 0)
        {
            var missingList = string.Join(", ", missing.Order(StringComparer.OrdinalIgnoreCase));
            throw new CakeException(
                $"ResolveVersionsFromManifest cannot resolve versions for requested scope entries not in manifest.json package_families[]: {missingList}." +
                "Add the families to manifest or narrow the --scope filter.");
        }

        return resolved;
    }

    private NuGetVersion BuildVersionFor(PackageFamilyConfig family, string suffix)
    {
        var library = _manifestConfig.LibraryManifests.SingleOrDefault(candidate =>
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
