using System.Collections.ObjectModel;

namespace Build.Context.Configs;

/// <summary>
/// Holds configuration settings specific to Vcpkg interactions.
/// </summary>
public class VcpkgConfiguration
{
    /// <summary>
    /// Gets the list of specific libraries to operate (e.g., "SDL2", "SDL2_image").
    /// Populated from command-line arguments (--library).
    /// </summary>
    public IReadOnlyList<string> Libraries { get; init; }

    public VcpkgConfiguration(IReadOnlyList<string>? libraries)
    {
        Libraries = new ReadOnlyCollection<string>(libraries?.ToList() ?? []);
    }
}
