using System.Collections.ObjectModel;
using Cake.Core.IO;

namespace Build.Context.Configs;

/// <summary>
/// Holds configuration settings specific to Vcpkg interactions.
/// </summary>
public class VcpkgConfiguration
{
    /// <summary>
    /// Gets the path to the Vcpkg installation directory.
    /// Populated from command-line argument (--vcpkg-dir) or determined dynamically.
    /// Will be resolved by PathService if initially null.
    /// </summary>
    public DirectoryPath? VcpkgRootPath { get; init; }

    /// <summary>
    /// Gets the mapping of features to enable per library per Vcpkg triplet.
    /// Example structure: { "sdl2-mixer": { "x64-windows-release": ["opusfile", "wavpack"] } }
    /// This will be populated later, possibly from a config file or arguments.
    /// </summary>
    public IReadOnlyDictionary<string, IReadOnlyDictionary<string, IReadOnlyList<string>>> FeatureMap { get; init; } =
        new ReadOnlyDictionary<string, IReadOnlyDictionary<string, IReadOnlyList<string>>>(
            new Dictionary<string, IReadOnlyDictionary<string, IReadOnlyList<string>>>(StringComparer.OrdinalIgnoreCase)
        );

    /// <summary>
    /// Gets the list of specific libraries to build (e.g., "SDL2", "SDL2_image").
    /// If empty, all libraries defined might be built.
    /// Populated from command-line arguments (--library).
    /// </summary>
    public IReadOnlyList<string> LibrariesToBuild { get; init; }

    public VcpkgConfiguration(DirectoryPath? vcpkgRootPath, IReadOnlyList<string>? librariesToBuild)
    {
        VcpkgRootPath = vcpkgRootPath;
        LibrariesToBuild = new ReadOnlyCollection<string>(librariesToBuild?.ToList() ?? []);
    }
}
