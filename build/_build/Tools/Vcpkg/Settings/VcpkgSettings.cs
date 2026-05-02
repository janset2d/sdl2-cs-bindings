using Cake.Core.IO;
using Cake.Core.Tooling;

namespace Build.Tools.Vcpkg.Settings;

/// <summary>
/// Base settings for Vcpkg tool operations.
/// Contains properties for common command-line switches.
/// </summary>
public class VcpkgSettings : ToolSettings
{
    private readonly DirectoryPath _vcpkgRoot;

    public VcpkgSettings(DirectoryPath vcpkgRoot)
    {
        _vcpkgRoot = vcpkgRoot;
    }

    public DirectoryPath VcpkgRoot => _vcpkgRoot;

    /// <summary>
    /// Gets or sets the target architecture triplet.
    /// Corresponds to the --triplet command line switch.
    /// This is typically determined by the build logic and passed to the tool settings.
    /// </summary>
    public string? Triplet { get; init; }

    /// <summary>
    /// Gets or sets the host architecture triplet.
    /// Corresponds to the --host-triplet command line switch.
    /// </summary>
    public string? HostTriplet { get; init; }

    /// <summary>
    /// Gets or sets the path where downloaded tools and source code archives should be kept.
    /// Corresponds to the --downloads-root command line switch.
    /// </summary>
    public DirectoryPath? DownloadsRoot { get; init; }

    /// <summary>
    /// Gets or sets a value indicating whether to force classic mode.
    /// Corresponds to the --classic command line switch.
    /// </summary>
    public bool? ClassicMode { get; init; }

    /// <summary>
    /// Gets or sets the list of overlay port directories.
    /// Corresponds to the --overlay-ports command line switch (can be specified multiple times).
    /// </summary>
    public IList<DirectoryPath> OverlayPorts { get; init; } = [];

    /// <summary>
    /// Gets or sets the list of overlay triplet directories.
    /// Corresponds to the --overlay-triplets command line switch (can be specified multiple times).
    /// </summary>
    public IList<DirectoryPath> OverlayTriplets { get; init; } = [];

    /// <summary>
    /// Gets or sets the list of binary caching sources.
    /// Corresponds to the --binarysource command line switch (can be specified multiple times).
    /// Note: The format of the configuration string might need specific handling.
    /// </summary>
    public IList<string> BinarySources { get; init; } = [];

    /// <summary>
    /// Gets or sets the list of feature flags to opt-in to experimental behavior.
    /// Corresponds to the --feature-flags command line switch (comma-separated list).
    /// </summary>
    public IList<string> FeatureFlags { get; init; } = [];

    // --- Experimental Options (Add if needed, use with caution) ---

    /// <summary>
    /// Gets or sets the temporary path for intermediate build files. EXPERIMENTAL.
    /// Corresponds to the --x-buildtrees-root command line switch.
    /// </summary>
    public DirectoryPath? BuildTreesRoot { get; init; }

    /// <summary>
    /// Gets or sets the path to lay out installed packages. EXPERIMENTAL.
    /// Corresponds to the --x-install-root command line switch.
    /// </summary>
    public DirectoryPath? InstallRoot { get; init; }

    /// <summary>
    /// Gets or sets the directory containing vcpkg.json. EXPERIMENTAL.
    /// Corresponds to the --x-manifest-root command line switch.
    /// </summary>
    public DirectoryPath? ManifestRoot { get; init; }

    /// <summary>
    /// Gets or sets the temporary path for intermediate package files. EXPERIMENTAL.
    /// Corresponds to the --x-packages-root command line switch.
    /// </summary>
    public DirectoryPath? PackagesRoot { get; init; }

    /// <summary>
    /// Gets or sets the cache configuration for Asset Caching. EXPERIMENTAL.
    /// Corresponds to the --x-asset-sources command line switch.
    /// </summary>
    public string? AssetSources { get; init; }

    // Note: Debugging flags (--x-cmake-debug, --x-cmake-configure-debug) are omitted
    // as they are complex and less common for typical build automation scenarios.
    // They could be added if required.
}
