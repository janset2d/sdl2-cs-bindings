using Cake.Core.IO;
using Cake.Core.Tooling;

namespace Build.Tools.Otool;

/// <summary>
/// Settings for the otool command which is used to display information about object files and libraries on macOS.
/// </summary>
public sealed class OtoolSettings : ToolSettings
{
    /// <summary>
    /// Initializes a new instance of the <see cref="OtoolSettings"/> class.
    /// </summary>
    /// <param name="filePath">The file path to analyze.</param>
    public OtoolSettings(FilePath filePath)
    {
        FilePath = filePath ?? throw new ArgumentNullException(nameof(filePath));
    }

    /// <summary>
    /// Gets the file path to analyze.
    /// </summary>
    public FilePath FilePath { get; }

    /// <summary>
    /// Gets or sets a value indicating whether to display the names and version numbers 
    /// of the shared libraries that the object file uses (-L flag).
    /// This is the most common use case for dependency analysis.
    /// </summary>
    public bool ShowLibraries { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether to display the load commands (-l flag).
    /// </summary>
    public bool ShowLoadCommands { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether to display the object file header (-h flag).
    /// </summary>
    public bool ShowHeader { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether to display verbose output (-v flag).
    /// </summary>
    public bool Verbose { get; set; }
} 