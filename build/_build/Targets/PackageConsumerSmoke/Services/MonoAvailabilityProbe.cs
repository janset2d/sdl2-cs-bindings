using Cake.Common;
using Cake.Common.IO;
using Cake.Core;
using Cake.Core.IO;

namespace Build.Targets.PackageConsumerSmoke.Services;

/// <summary>
/// Probes <c>$PATH</c> for a <c>mono</c> executable using Cake's environment + filesystem
/// abstractions (no <c>System.IO</c> in build host by contract). Used by
/// <c>PackageConsumerSmokeTask.ShouldSkipTfm</c> to decide whether net4x runtime execution
/// can proceed on non-Windows hosts. Failure surfaces as a clean skip + reason instead of
/// an opaque MTP "Runner 'mono' not found" stack trace.
/// </summary>
public sealed class MonoAvailabilityProbe(ICakeContext cakeContext)
{
    private readonly ICakeContext _cakeContext = cakeContext ?? throw new ArgumentNullException(nameof(cakeContext));

    public bool IsMonoAvailable()
    {
        var pathEnv = _cakeContext.EnvironmentVariable("PATH");
        if (string.IsNullOrEmpty(pathEnv))
        {
            return false;
        }

        // PATH separator is ':' on Unix and ';' on Windows. Use Cake's environment
        // abstraction so tests with a fake Unix environment behave correctly even when
        // the host process happens to be running on Windows.
        var separator = _cakeContext.Environment.Platform.IsUnix() ? ':' : ';';

        foreach (var dir in pathEnv.Split(separator))
        {
            if (string.IsNullOrEmpty(dir))
            {
                continue;
            }

            FilePath candidate;
            try
            {
                candidate = new DirectoryPath(dir).CombineWithFilePath("mono");
            }
            catch (ArgumentException)
            {
                // Malformed PATH entry — skip and keep probing the rest.
                continue;
            }

            if (_cakeContext.FileExists(candidate))
            {
                return true;
            }
        }

        return false;
    }
}
