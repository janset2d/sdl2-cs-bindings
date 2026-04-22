using System.Globalization;
using Build.Context.Models;
using Cake.Core;
using NuGet.Versioning;

namespace Build.Application.Versioning;

/// <summary>
/// <see cref="IPackageVersionProvider"/> that derives per-family versions from
/// <c>manifest.json library_manifests[].vcpkg_version</c> upstream major/minor plus a
/// caller-supplied suffix. Output shape is
/// <c>&lt;UpstreamMajor&gt;.&lt;UpstreamMinor&gt;.0-&lt;suffix&gt;</c> — e.g.,
/// <c>2.32.0-local.20260421T143022</c> for <c>sdl2-core</c> with
/// <c>suffix=local.20260421T143022</c> when SDL2 upstream is <c>2.32.10</c>.
/// <para>
/// Callers compose the suffix from their context: <c>local.&lt;timestamp&gt;</c> for
/// developer workflows, <c>ci.&lt;run-id&gt;.&lt;run-attempt&gt;</c> for CI manifest-derived
/// runs, <c>pa2.&lt;run-id&gt;</c> for PA-2 witness runs. Provider does not know about
/// workflow semantics; it only stamps the given suffix onto the upstream prefix.
/// </para>
/// <para>
/// This provider is never reachable from a stage task's CLI; per ADR-003 §3.1 stage tasks
/// consume only the <see cref="ExplicitVersionProvider"/> output. <c>ManifestVersionProvider</c>
/// is instantiated inside <c>LocalArtifactSourceResolver.PrepareFeedAsync</c> (local-dev
/// suffix) and inside the <c>ResolveVersions</c> task runner (CI manifest-derived mode),
/// where the resolved mapping is then either handed to stage runners in-process or
/// serialized to JSON for CI <c>needs:</c> consumption.
/// </para>
/// </summary>
public sealed class ManifestVersionProvider(
    ManifestConfig manifestConfig,
    string suffix) : IPackageVersionProvider
{
    private readonly ManifestConfig _manifestConfig = manifestConfig ?? throw new ArgumentNullException(nameof(manifestConfig));
    private readonly string _suffix = NormalizeSuffix(suffix);

    public Task<IReadOnlyDictionary<string, NuGetVersion>> ResolveAsync(
        IReadOnlySet<string> requestedScope,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(requestedScope);
        cancellationToken.ThrowIfCancellationRequested();

        var familiesToResolve = ResolveFamiliesInScope(requestedScope);

        var mapping = new Dictionary<string, NuGetVersion>(familiesToResolve.Count, StringComparer.OrdinalIgnoreCase);
        foreach (var family in familiesToResolve)
        {
            cancellationToken.ThrowIfCancellationRequested();
            mapping[family.Name] = BuildVersionFor(family);
        }

        return Task.FromResult<IReadOnlyDictionary<string, NuGetVersion>>(mapping);
    }

    private List<PackageFamilyConfig> ResolveFamiliesInScope(IReadOnlySet<string> requestedScope)
    {
        if (requestedScope.Count == 0)
        {
            if (_manifestConfig.PackageFamilies.Count == 0)
            {
                throw new CakeException(
                    "ManifestVersionProvider cannot resolve versions: manifest.json package_families[] is empty. " +
                    "Declare at least one family before invoking ResolveVersions in manifest mode.");
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
            var missingList = string.Join(", ", missing.OrderBy(name => name, StringComparer.OrdinalIgnoreCase));
            throw new CakeException(
                "ManifestVersionProvider cannot resolve versions for requested scope entries not in " +
                "manifest.json package_families[]: " + missingList +
                ". Add the families to manifest or narrow the scope.");
        }

        return resolved;
    }

    private NuGetVersion BuildVersionFor(PackageFamilyConfig family)
    {
        var library = _manifestConfig.LibraryManifests.SingleOrDefault(candidate =>
            string.Equals(candidate.Name, family.LibraryRef, StringComparison.OrdinalIgnoreCase));

        if (library is null)
        {
            throw new CakeException(
                $"ManifestVersionProvider cannot resolve version for family '{family.Name}' because " +
                $"library_ref '{family.LibraryRef}' does not exist in manifest library_manifests[].");
        }

        if (!NuGetVersion.TryParse(library.VcpkgVersion, out var upstreamVersion))
        {
            throw new CakeException(
                $"ManifestVersionProvider cannot resolve version for family '{family.Name}' because " +
                $"library '{library.Name}' has non-semantic vcpkg_version '{library.VcpkgVersion}'.");
        }

        var candidate = string.Create(
            CultureInfo.InvariantCulture,
            $"{upstreamVersion.Major}.{upstreamVersion.Minor}.0-{_suffix}");

        if (!NuGetVersion.TryParse(candidate, out var composed))
        {
            throw new CakeException(
                $"ManifestVersionProvider produced an invalid NuGet SemVer for family '{family.Name}': " +
                $"'{candidate}'. Suffix '{_suffix}' likely contains characters disallowed in a prerelease " +
                "identifier (allowed: ASCII alphanumerics, hyphens, dot-separated segments).");
        }

        return composed;
    }

    private static string NormalizeSuffix(string? suffix)
    {
        if (string.IsNullOrWhiteSpace(suffix))
        {
            throw new ArgumentException(
                "ManifestVersionProvider requires a non-empty suffix (for example 'local.<timestamp>', " +
                "'ci.<run-id>', or 'pa2.<run-id>'). Empty suffixes would produce a non-prerelease version " +
                "that looks like a stable release but is derived purely from manifest metadata.",
                nameof(suffix));
        }

        return suffix.Trim();
    }
}
