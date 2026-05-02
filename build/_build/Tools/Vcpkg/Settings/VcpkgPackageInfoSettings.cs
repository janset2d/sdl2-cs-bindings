using Cake.Core.IO;

namespace Build.Tools.Vcpkg.Settings;

/// <summary>
/// Contains settings specific to the 'vcpkg x-package-info' command.
/// </summary>
public class VcpkgPackageInfoSettings : VcpkgSettings
{
    public VcpkgPackageInfoSettings(DirectoryPath vcpkgRoot) : base(vcpkgRoot)
    {
    }

    /// <summary>
    /// Gets or sets a value indicating whether to report installed packages rather than available ones. EXPERIMENTAL.
    /// Corresponds to the --x-installed flag.
    /// </summary>
    public bool Installed { get; init; }

    /// <summary>
    /// Gets or sets a value indicating whether to also report dependencies of installed packages. EXPERIMENTAL.
    /// Requires --x-installed to be true.
    /// Corresponds to the --x-transitive flag.
    /// </summary>
    public bool Transitive { get; init; }

    /// <summary>
    /// Gets or sets a value indicating whether to print JSON output.
    /// This appears to be mandatory for the command to succeed.
    /// Defaults to true. Corresponds to the --x-json flag.
    /// </summary>
    public bool JsonOutput { get; init; } = true; // Default to true as it seems required
}
