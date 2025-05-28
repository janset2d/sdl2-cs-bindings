using System.Collections.ObjectModel;
using OneOf.Monads;

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
    /// Gets the Runtime Identifier (RID) used to specify the target platform for package restoration in Vcpkg.
    /// Provides an optional value that reflects the system's runtime characteristics.
    /// </summary>
    public Option<string> Rid { get; init; }

    public VcpkgConfiguration(IReadOnlyList<string>? libraries,  string? rid)
    {
        Libraries = new ReadOnlyCollection<string>(libraries?.ToList() ?? []);
        Rid = string.IsNullOrWhiteSpace(rid) ? Option<string>.None() : rid;
    }
}
