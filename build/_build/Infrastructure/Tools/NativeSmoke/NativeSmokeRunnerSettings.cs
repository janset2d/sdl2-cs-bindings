using Cake.Core.IO;
using Cake.Core.Tooling;

namespace Build.Infrastructure.Tools.NativeSmoke;

/// <summary>
/// Settings for running the native-smoke executable produced by the CMake harness.
/// </summary>
public sealed class NativeSmokeRunnerSettings(FilePath executablePath) : ToolSettings
{
    /// <summary>
    /// Full path to the native-smoke executable.
    /// </summary>
    public FilePath ExecutablePath { get; } = executablePath ?? throw new ArgumentNullException(nameof(executablePath));

    /// <summary>
    /// Optional argument list passed to native-smoke.
    /// </summary>
    public IList<string> Arguments { get; } = [];
}
