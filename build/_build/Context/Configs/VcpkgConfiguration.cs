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

    /// <summary>
    /// Gets the list of specific RID to operate (e.g., "win-x64", "linux-x64").
    /// Populated from command-line arguments (--rid).
    /// </summary>
    public string? Rid { get; init; }

    public VcpkgConfiguration(IReadOnlyList<string>? libraries, string? rid)
    {
        Libraries = new ReadOnlyCollection<string>(libraries?.ToList() ?? []);
        Rid = rid;
    }
}
