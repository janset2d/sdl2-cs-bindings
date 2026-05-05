using Build.Host.Configuration;
using Build.Host.Paths;
using Build.Shared.Manifest;
using Build.Shared.Runtime;
using Cake.Core;
using Cake.Core.IO;
using Cake.Frosting;

namespace Build.Host;

/// <summary>
/// Cake/Frosting invocation state for the build host. Carries the four orthogonal axes
/// every Cake task or pipeline composes its work against — repo / artifact paths, the
/// runtime/RID profile, the loaded manifest, and operator-supplied options. The surface
/// is intentionally narrow: data + ambient Cake API, never a service locator. Behavior
/// lives in <see cref="Features"/>; cross-feature vocabulary in <see cref="Shared"/>;
/// CLI tool wrappers in <see cref="Tools"/>; non-Cake adapters in <see cref="Integrations"/>.
/// </summary>
public sealed class BuildContext : FrostingContext
{
    public BuildContext(
        ICakeContext context,
        IPathService pathService,
        IRuntimeProfile runtimeProfile,
        ManifestConfig manifest,
        ParsedArguments parsedArguments,
        Configurations options)
        : base(context)
    {
        Paths = pathService ?? throw new ArgumentNullException(nameof(pathService));
        Runtime = runtimeProfile ?? throw new ArgumentNullException(nameof(runtimeProfile));
        Manifest = manifest ?? throw new ArgumentNullException(nameof(manifest));
        ParsedArguments = parsedArguments ?? throw new ArgumentNullException(nameof(parsedArguments));
        Options = options ?? throw new ArgumentNullException(nameof(options));
    }

    /// <summary>Repo / artifact / harvest layout knowledge. Cake-aware (carries DirectoryPath / FilePath).</summary>
    public IPathService Paths { get; }

    /// <summary>Active RID profile (RID, triplet, system-exclusion list, host-vs-target invariants).</summary>
    public IRuntimeProfile Runtime { get; }

    /// <summary>
    /// Loaded <c>build/manifest.json</c> as data. Read-only access only;
    /// helpers like <c>ResolveConcreteFamilies()</c> live in <c>Shared/PackageFamilies/</c>
    /// extensions, not on this carrier.
    /// </summary>
    public ManifestConfig Manifest { get; }

    /// <summary>
    /// Parsed CLI arguments. Set once at composition time; invocation state is immutable
    /// thereafter — tasks read but never mutate.
    /// </summary>
    public ParsedArguments ParsedArguments { get; }

    /// <summary>
    /// Aggregate of operator-input axes (Vcpkg, Package, Versioning, Repository, DotNet,
    /// Dumpbin) normalized from CLI args at composition time. Per-axis sub-records remain
    /// individually DI-injectable for services that only need a single slice.
    /// </summary>
    public Configurations Options { get; }

    // ── Named CLI properties (ADR-002 §6) ──

    /// <summary>Resolved runtime identifier (e.g. "win-x64").</summary>
    public string RuntimeIdentifier => Runtime.Rid;

    /// <summary>Build configuration from --config CLI option. Default is "Release".</summary>
    public string BuildConfiguration => ParsedArguments.Config;

    /// <summary>Path to the resolved versions file (--versions-file or default output path).</summary>
    public FilePath VersionsFilePath =>
        !string.IsNullOrWhiteSpace(ParsedArguments.VersionsFile)
            ? new FilePath(ParsedArguments.VersionsFile!)
            : Paths.GetResolveVersionsOutputFile();
}
