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
    private readonly string _buildConfiguration;
    private readonly FilePath? _versionsFilePath;
    private readonly string? _resolveVersionsSuffix;
    private readonly IReadOnlyList<string> _resolveVersionsScope;
    private readonly IReadOnlyList<string> _explicitVersionEntries;
    private readonly string? _explicitVersions;
    private readonly IReadOnlyList<string> _dlls;
    private readonly IReadOnlyList<string> _libraries;

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
        Options = options ?? throw new ArgumentNullException(nameof(options));

        ArgumentNullException.ThrowIfNull(parsedArguments);

        _buildConfiguration = parsedArguments.Config;
        _versionsFilePath = !string.IsNullOrWhiteSpace(parsedArguments.VersionsFile)
            ? new FilePath(parsedArguments.VersionsFile!)
            : null;
        _resolveVersionsSuffix = parsedArguments.Suffix;
        _resolveVersionsScope = [.. parsedArguments.Scope];
        _explicitVersionEntries = [.. parsedArguments.ExplicitVersion];
        _explicitVersions = parsedArguments.ExplicitVersions;
        _dlls = [.. parsedArguments.Dll];
        _libraries = [.. parsedArguments.Library];
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
     /// Aggregate of operator-input axes (Vcpkg, Package, Versioning, Repository, DotNet,
     /// Dumpbin) normalized from CLI args at composition time. Per-axis sub-records remain
    /// individually DI-injectable for services that only need a single slice.
    /// </summary>
    public Configurations Options { get; }

    // ── Named CLI properties (ADR-002 §6) ──

    /// <summary>Resolved runtime identifier (e.g. "win-x64").</summary>
    public string RuntimeIdentifier => Runtime.Rid;

    /// <summary>Build configuration from --config CLI option. Default is "Release".</summary>
    public string BuildConfiguration => _buildConfiguration;

    /// <summary>Path to the resolved versions file from --versions-file. Null when not supplied; tasks must validate.</summary>
    public FilePath? VersionsFilePath => _versionsFilePath;

    /// <summary>Prerelease suffix consumed by ResolveVersionsFromManifest.</summary>
    public string? ResolveVersionsSuffix => _resolveVersionsSuffix;

    /// <summary>Family scope filter consumed by ResolveVersionsFromManifest. Empty means all families.</summary>
    public IReadOnlyList<string> ResolveVersionsScope => _resolveVersionsScope;

    /// <summary>Repeated family-version entries consumed by ResolveVersionsFromExplicit.</summary>
    public IReadOnlyList<string> ExplicitVersionEntries => _explicitVersionEntries;

    /// <summary>Comma-separated family-version entries consumed by ResolveVersionsFromExplicit.</summary>
    public string? ExplicitVersions => _explicitVersions;

    /// <summary>Operator-supplied --dll list. Consumed by Dumpbin-Dependents, Ldd-Dependents, Otool-Analyze.</summary>
    public IReadOnlyList<string> Dlls => _dlls;

    /// <summary>Operator-supplied --library list. Consumed by Inspect-HarvestedDependencies.</summary>
    public IReadOnlyList<string> Libraries => _libraries;
}
