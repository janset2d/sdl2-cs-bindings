using Build.Host.Configuration;
using Build.Host.Paths;
using Build.Shared.Manifest;
using Build.Shared.Runtime;
using Cake.Core;
using Cake.Frosting;

namespace Build.Host;

/// <summary>
/// Cake/Frosting invocation state for the build host. Carries the four orthogonal axes
/// every Cake task or pipeline composes its work against — repo / artifact paths, the
/// runtime/RID profile, the loaded manifest, and operator-supplied options. Per ADR-004
/// §2.11 the surface is intentionally narrow: data + ambient Cake API, never a service
/// locator. Behavior lives in <see cref="Features"/>; cross-feature vocabulary in
/// <see cref="Shared"/>; CLI tool wrappers in <see cref="Tools"/>; non-Cake adapters in
/// <see cref="Integrations"/>.
/// </summary>
public sealed class BuildContext : FrostingContext
{
    public BuildContext(
        ICakeContext context,
        IPathService pathService,
        IRuntimeProfile runtimeProfile,
        ManifestConfig manifest,
        BuildOptions options)
        : base(context)
    {
        Paths = pathService ?? throw new ArgumentNullException(nameof(pathService));
        Runtime = runtimeProfile ?? throw new ArgumentNullException(nameof(runtimeProfile));
        Manifest = manifest ?? throw new ArgumentNullException(nameof(manifest));
        Options = options ?? throw new ArgumentNullException(nameof(options));
    }

    /// <summary>Repo / artifact / harvest layout knowledge. Cake-aware (carries DirectoryPath / FilePath).</summary>
    public IPathService Paths { get; }

    /// <summary>Active RID profile (RID, triplet, system-exclusion list, host-vs-target invariants).</summary>
    public IRuntimeProfile Runtime { get; }

    /// <summary>
    /// Loaded <c>build/manifest.json</c> as data. Per ADR-004 §2.11 read-only access only;
    /// helpers like <c>ResolveConcreteFamilies()</c> live in <c>Shared/PackageFamilies/</c>
    /// extensions, not on this carrier.
    /// </summary>
    public ManifestConfig Manifest { get; }

    /// <summary>
    /// Aggregate of operator-input axes (Vcpkg, Package, Versioning, Repository, DotNet,
    /// Dumpbin) normalized from CLI args at composition time. Per-axis sub-records remain
    /// individually DI-injectable for services that only need a single slice.
    /// </summary>
    public BuildOptions Options { get; }
}
