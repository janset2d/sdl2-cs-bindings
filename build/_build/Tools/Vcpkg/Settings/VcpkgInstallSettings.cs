namespace Build.Tools.Vcpkg.Settings;

/// <summary>
/// Contains settings specific to the 'vcpkg install' command.
/// </summary>
public class VcpkgInstallSettings : VcpkgSettings
{
    /// <summary>
    /// Gets or sets a value indicating whether to allow installation even if a port is unsupported.
    /// Corresponds to the --allow-unsupported flag.
    /// </summary>
    public bool AllowUnsupported { get; init; }

    /// <summary>
    /// Gets or sets a value indicating whether to clean buildtrees, packages, and downloads after building each package.
    /// Corresponds to the --clean-after-build flag.
    /// </summary>
    public bool CleanAfterBuild { get; init; }

    /// <summary>
    /// Gets or sets a value indicating whether to clean buildtrees after building each package.
    /// Corresponds to the --clean-buildtrees-after-build flag.
    /// </summary>
    public bool CleanBuildTreesAfterBuild { get; init; }

    /// <summary>
    /// Gets or sets a value indicating whether to clean downloads after building each package.
    /// Corresponds to the --clean-downloads-after-build flag.
    /// </summary>
    public bool CleanDownloadsAfterBuild { get; init; }

    /// <summary>
    /// Gets or sets a value indicating whether to clean packages after building each package.
    /// Corresponds to the --clean-packages-after-build flag.
    /// </summary>
    public bool CleanPackagesAfterBuild { get; init; }

    /// <summary>
    /// Gets or sets a value indicating whether to print the install plan without installing packages.
    /// Corresponds to the --dry-run flag.
    /// </summary>
    public bool DryRun { get; init; }

    /// <summary>
    /// Gets or sets a value indicating whether to perform editable builds (Classic Mode only).
    /// Corresponds to the --editable flag.
    /// </summary>
    /// <remarks>Classic Mode only.</remarks>
    public bool Editable { get; init; }

    /// <summary>
    /// Gets or sets a value indicating whether to fail if a port has detected problems.
    /// Corresponds to the --enforce-port-checks flag.
    /// </summary>
    public bool EnforcePortChecks { get; init; }

    /// <summary>
    /// Gets or sets the list of additional features to install dependencies for (Manifest Mode only). EXPERIMENTAL.
    /// Corresponds to the --x-feature flag (can be specified multiple times).
    /// </summary>
    /// <remarks>Manifest Mode only. Experimental.</remarks>
    public IList<string> Features { get; init; } = [];

    /// <summary>
    /// Gets or sets a value indicating whether to fetch the latest sources (Classic Mode only).
    /// Corresponds to the --head flag.
    /// </summary>
    /// <remarks>Classic Mode only.</remarks>
    public bool Head { get; init; }

    /// <summary>
    /// Gets or sets a value indicating whether to continue the install plan after the first failure.
    /// Corresponds to the --keep-going flag.
    /// </summary>
    public bool KeepGoing { get; init; }

    /// <summary>
    /// Gets or sets a value indicating whether to skip installing default features (Manifest Mode only). EXPERIMENTAL.
    /// Corresponds to the --x-no-default-features flag.
    /// </summary>
    /// <remarks>Manifest Mode only. Experimental.</remarks>
    public bool NoDefaultFeatures { get; init; }

    /// <summary>
    /// Gets or sets a value indicating whether to prevent ports from downloading new assets.
    /// Corresponds to the --no-downloads flag.
    /// </summary>
    public bool NoDownloads { get; init; }

    /// <summary>
    /// Gets or sets a value indicating whether to only download assets without building.
    /// Corresponds to the --only-downloads flag.
    /// </summary>
    public bool OnlyDownloads { get; init; }

    /// <summary>
    /// Gets or sets a value indicating whether to only restore packages from binary caches without building.
    /// Corresponds to the --only-binarycaching flag.
    /// </summary>
    public bool OnlyBinaryCaching { get; init; }

    /// <summary>
    /// Gets or sets a value indicating whether to approve plans that require rebuilding packages (Classic Mode only).
    /// Corresponds to the --recurse flag.
    /// </summary>
    /// <remarks>Classic Mode only.</remarks>
    public bool Recurse { get; init; }

    /// <summary>
    /// Gets or sets a value indicating whether to write a NuGet packages.config file. EXPERIMENTAL.
    /// Corresponds to the --x-write-nuget-packages-config flag.
    /// </summary>
    /// <remarks>Experimental.</remarks>
    public bool WriteNuGetPackagesConfig { get; init; }

    /// <summary>
    /// Gets or sets a value indicating whether to suppress generation of usage text.
    /// Corresponds to the --no-print-usage flag.
    /// </summary>
    public bool NoPrintUsage { get; init; }
}
