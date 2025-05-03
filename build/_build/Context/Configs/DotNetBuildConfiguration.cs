namespace Build.Context.Configs;

/// <summary>
/// Holds general build configuration settings.
/// </summary>
public class DotNetBuildConfiguration
{
    /// <summary>
    /// Gets the target build configuration (e.g., "Release", "Debug").
    /// Populated from command-line or defaults to "Release".
    /// </summary>
    public string Configuration { get; init; }

    // Constructor to allow setting properties via DI or initialization logic
    public DotNetBuildConfiguration(string configuration)
    {
        Configuration = configuration;
    }
}
