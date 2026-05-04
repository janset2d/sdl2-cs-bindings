namespace Build.Host.Configuration;

/// <summary>
/// Aggregate record carrying every per-run, operator-input-derived configuration axis the
/// build host consumes. Single point of access for tasks and pipelines that want one
/// composed surface (<c>context.Options</c>) instead of independent injected configuration
/// types. Each member is a thin record whose values were normalized in <c>Program.cs</c>
/// from parsed CLI arguments.
/// <para>
/// The aggregate is part of the slimmed <see cref="BuildContext"/>
/// surface (<c>Paths</c> / <c>Runtime</c> / <c>Manifest</c> / <c>Options</c>) — composition
/// root builds it once at startup and registers it as a singleton; tasks read
/// <c>context.Options.Vcpkg.Libraries</c> et cetera. Direct DI injection of an individual
/// sub-record (e.g. <see cref="VcpkgConfiguration"/>) remains valid for services that
/// only need that axis.
/// </para>
/// <para>
/// The <c>Versioning</c> slot was retired in plan v4 (versioning split): tasks
/// <c>ResolveVersionsFromManifest</c> and <c>ResolveVersionsFromExplicit</c> read
/// <see cref="BuildContext.ParsedArguments"/> directly. The Configuration record pattern
/// is being retired feature-by-feature; remaining slots stay until each owning feature
/// migrates to direct ParsedArguments reads.
/// </para>
/// </summary>
/// <remarks>
/// The <c>Dumpbin</c> sub-record is named after the underlying tool (<c>--dll</c> arg
/// surface for <c>Dumpbin-Dependents</c>, also reused by <c>Ldd-Dependents</c> and
/// <c>Otool-Analyze</c>). Naming alignment with the broader "Diagnostics" axis is deferred
/// to a future naming cleanup.
/// </remarks>
public sealed record Configurations(
    VcpkgConfiguration Vcpkg,
    PackageBuildConfiguration Package,
    RepositoryConfiguration Repository,
    DotNetBuildConfiguration DotNet,
    DumpbinConfiguration Dumpbin);
