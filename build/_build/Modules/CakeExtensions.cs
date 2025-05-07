using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using Cake.Common.IO;
using Cake.Core;
using Cake.Core.IO;

namespace Build.Modules;

public static class CakeExtensions
{
    public static string Rid(this ICakePlatform platform)
    {
        ArgumentNullException.ThrowIfNull(platform);

        string osPart = platform.Family switch
        {
            PlatformFamily.Windows => "win",
            PlatformFamily.Linux => "linux",
            PlatformFamily.OSX => "osx",
            _ => throw new PlatformNotSupportedException("Cannot determine OS platform for RID."),
        };
        string archPart = RuntimeInformation.OSArchitecture switch
        {
            Architecture.X64 => "x64",
            Architecture.Arm64 => "arm64",
            Architecture.X86 => "x86",
            Architecture.Arm => "arm",
            _ => throw new PlatformNotSupportedException($"Cannot determine OS architecture {RuntimeInformation.OSArchitecture} for RID."),
        };

        return $"{osPart}-{archPart}";
    }

    public static async Task<TModel> ToJsonAsync<TModel>(this ICakeContext cakeContext, FilePath filePath)
    {
        ArgumentNullException.ThrowIfNull(cakeContext);
        ArgumentNullException.ThrowIfNull(filePath);

        if (!cakeContext.FileExists(filePath))
        {
            throw new CakeException($"File not found at: {filePath.FullPath}");
        }

        if (!string.Equals(filePath.GetExtension(), ".json", StringComparison.OrdinalIgnoreCase))
        {
            throw new CakeException($"File extension must be '.json': {filePath.FullPath}");
        }

        TModel? model;
        try
        {
            await using var stream = File.OpenRead(filePath.FullPath);

            if (stream.Length == 0)
            {
                throw new CakeException($"File is empty: {filePath.FullPath}");
            }

            model = await JsonSerializer.DeserializeAsync<TModel>(stream);
        }
        catch (JsonException ex)
        {
            throw new CakeException($"Error deserializing JSON from file {filePath.FullPath}: {ex.Message}", ex);
        }
        catch (IOException ex)
        {
            throw new CakeException($"Error reading file {filePath.FullPath}: {ex.Message}", ex);
        }

        if (model == null)
        {
            throw new CakeException($"Failed to deserialize to {typeof(TModel).FullName} from {filePath.FullPath}. JSON content might be 'null' or deserialization resulted in null.");
        }

        return model;
    }
}

public static class Rids
{
    // Windows RIDs
    public const string WinX64 = "win-x64";
    public const string WinX86 = "win-x86";
    public const string WinArm64 = "win-arm64";

    // Linux RIDs
    public const string LinuxX64 = "linux-x64";
    public const string LinuxArm64 = "linux-arm64";

    // macOS RIDs
    public const string OsxX64 = "osx-x64";
    public const string OsxArm64 = "osx-arm64";
}
