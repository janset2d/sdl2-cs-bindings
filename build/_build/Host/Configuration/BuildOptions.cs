namespace Build.Host.Configuration;

/// <summary>
/// Aggregate record carrying every per-run, operator-input-derived configuration axis the
/// build host consumes. Single point of access for tasks and pipelines that want one
/// composed surface (<c>context.Options</c>) instead of six independent injected
/// configuration types. Each member is a thin record whose values were normalized in
/// <c>Program.cs</c> from parsed CLI arguments.
/// <para>
/// Per ADR-004 §2.11.1, the aggregate is part of the slimmed <see cref="BuildContext"/>
/// surface (<c>Paths</c> / <c>Runtime</c> / <c>Manifest</c> / <c>Options</c>) — composition
/// root builds it once at startup and registers it as a singleton; tasks read
/// <c>context.Options.Vcpkg.Libraries</c> et cetera. Direct DI injection of an individual
/// sub-record (e.g. <see cref="VcpkgConfiguration"/>) remains valid for services that
/// only need that axis.
/// </para>
/// </summary>
/// <remarks>
/// The <c>Dumpbin</c> sub-record is named after the underlying tool (<c>--dll</c> arg
/// surface for <c>Dumpbin-Dependents</c>, also reused by <c>Ldd-Dependents</c> and
/// <c>Otool-Analyze</c>). Naming alignment with the broader "Diagnostics" axis named in
/// ADR-004 §2.11.1 is deferred to P5 naming cleanup.
/// </remarks>
public sealed record BuildOptions(
    VcpkgConfiguration Vcpkg,
    PackageBuildConfiguration Package,
    VersioningConfiguration Versioning,
    RepositoryConfiguration Repository,
    DotNetBuildConfiguration DotNet,
    DumpbinConfiguration Dumpbin);
