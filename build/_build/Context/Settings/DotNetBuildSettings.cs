namespace Build.Context.Settings;

/// <summary>
/// Holds general build configuration settings.
/// </summary>
public class DotNetBuildSettings
{
    /// <summary>
    /// Gets the target build configuration (e.g., "Release", "Debug").
    /// Populated from command-line or defaults to "Release".
    /// </summary>
    public string Configuration { get; init; }

    // Constructor to allow setting properties via DI or initialization logic
    public DotNetBuildSettings(string configuration)
    {
        Configuration = configuration;
    }
}
