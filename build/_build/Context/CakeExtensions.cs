using System.Runtime.InteropServices;
using System.IO;
using System.Text.Json;
using Cake.Common.IO;
using Cake.Core;
using Cake.Core.IO;

namespace Build.Context;

public static class CakeExtensions
{
    public static string Rid(this ICakePlatform platform)
    {
        ArgumentNullException.ThrowIfNull(platform);

        var osPart = platform.Family switch
        {
            PlatformFamily.Windows => "win",
            PlatformFamily.Linux => "linux",
            PlatformFamily.OSX => "osx",
            _ => throw new PlatformNotSupportedException("Cannot determine OS platform for RID."),
        };
        var archPart = RuntimeInformation.OSArchitecture switch
        {
            Architecture.X64 => "x64",
            Architecture.Arm64 => "arm64",
            Architecture.X86 => "x86",
            Architecture.Arm => "arm",
            _ => throw new PlatformNotSupportedException($"Cannot determine OS architecture {RuntimeInformation.OSArchitecture} for RID."),
        };

        return $"{osPart}-{archPart}";
    }

    public static TModel ToJson<TModel>(this ICakeContext cakeContext, FilePath filePath)
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
            var file = cakeContext.FileSystem.GetFile(filePath);
            using var stream = file.OpenRead();

            if (stream.Length == 0)
            {
                throw new CakeException($"File is empty: {filePath.FullPath}");
            }

            model = JsonSerializer.Deserialize<TModel>(stream);
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
            var file = cakeContext.FileSystem.GetFile(filePath);
            await using var stream = file.OpenRead();

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

    public static async Task<string> ReadAllTextAsync(this ICakeContext cakeContext, FilePath filePath)
    {
        ArgumentNullException.ThrowIfNull(cakeContext);
        ArgumentNullException.ThrowIfNull(filePath);

        if (!cakeContext.FileExists(filePath))
        {
            throw new CakeException($"File not found at: {filePath.FullPath}");
        }

        var file = cakeContext.FileSystem.GetFile(filePath);
        using var stream = file.OpenRead();
        using var reader = new StreamReader(stream);
        return await reader.ReadToEndAsync();
    }

    public static async Task WriteAllTextAsync(this ICakeContext cakeContext, FilePath filePath, string content)
    {
        ArgumentNullException.ThrowIfNull(cakeContext);
        ArgumentNullException.ThrowIfNull(filePath);
        ArgumentNullException.ThrowIfNull(content);

        cakeContext.EnsureDirectoryExists(filePath.GetDirectory());

        var file = cakeContext.FileSystem.GetFile(filePath);
        await using var stream = file.Open(FileMode.Create, FileAccess.Write, FileShare.None);
        await using var writer = new StreamWriter(stream);
        await writer.WriteAsync(content);
    }
}
