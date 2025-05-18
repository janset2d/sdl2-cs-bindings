using Cake.Core.IO;
using Cake.Core.Tooling;

namespace Build.Tools.Ldd;

/// <summary>
/// Settings for the ldd tool which is used to print shared library dependencies.
/// </summary>
public sealed class LddSettings : ToolSettings
{
    /// <summary>
    /// Initializes a new instance of the <see cref="LddSettings"/> class.
    /// </summary>
    /// <param name="filePath">The file path to analyze.</param>
    public LddSettings(FilePath filePath)
    {
        FilePath = filePath ?? throw new ArgumentNullException(nameof(filePath));
    }

    /// <summary>
    /// Gets the file path to analyze.
    /// </summary>
    public FilePath FilePath { get; }

    /// <summary>
    /// Gets or sets a value indicating whether to show unused direct dependencies (-u flag).
    /// </summary>
    public bool ShowUnused { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether to perform relocations (-r flag).
    /// </summary>
    public bool PerformRelocations { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether to include data dependencies (-d flag).
    /// </summary>
    public bool IncludeData { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether to use verbose output (-v flag).
    /// </summary>
    public bool Verbose { get; set; }
}
