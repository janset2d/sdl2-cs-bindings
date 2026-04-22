using System.Runtime.InteropServices;
using System.Text.Json;
using Cake.Common.IO;
using Cake.Core;
using Cake.Core.IO;

namespace Build.Context;

public static class CakeExtensions
{
    /// <summary>
    /// Project-wide default JSON serialization options for <see cref="WriteJsonAsync{TModel}"/>
    /// callers that do not need a domain-specific shape (e.g., HarvestJsonContract.Options).
    /// Indented for human inspection of the emitted file; other settings stay System.Text.Json
    /// defaults (PascalCase, UTF-8, no escape-tightening).
    /// </summary>
    public static readonly JsonSerializerOptions DefaultJsonOptions = new()
    {
        WriteIndented = true,
    };

    /// <summary>
    /// Resolves the current host's .NET runtime identifier (<c>win-x64</c>, <c>linux-arm64</c>, <c>osx-x64</c>, …)
    /// from <see cref="ICakePlatform.Family"/> plus <see cref="RuntimeInformation.OSArchitecture"/>. This is the
    /// default value PathService consults when <c>--rid</c> is omitted, and the anchor for Harvest's per-RID
    /// deployment layout. Throws <see cref="PlatformNotSupportedException"/> on unrecognized OS family or CPU
    /// architecture so unsupported hosts fail loud rather than silently deriving a bogus RID string.
    /// </summary>
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

    /// <summary>
    /// Cake-native synchronous JSON read. Opens <paramref name="filePath"/> via
    /// <see cref="IFileSystem"/>, deserializes the contents into <typeparamref name="TModel"/>,
    /// and returns the populated model. Throws <see cref="CakeException"/> for missing file,
    /// non-<c>.json</c> extension, empty content, deserialization error, or <see langword="null"/>
    /// model — every failure surfaces as a Cake-typed error rather than a raw I/O / JSON exception.
    /// Prefer <see cref="ToJsonAsync{TModel}"/> in async contexts; this overload exists for the
    /// minority of DI factory callsites that run synchronously during composition-root setup.
    /// </summary>
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

    /// <summary>
    /// Cake-native asynchronous JSON read. Mirror of <see cref="ToJson{TModel}"/> backed by
    /// <see cref="JsonSerializer.DeserializeAsync{TValue}(Stream, JsonSerializerOptions?, CancellationToken)"/>.
    /// Callers pass their own <see cref="JsonSerializerOptions"/> when they need a domain-specific
    /// shape (naming policy, converters, case-insensitivity); otherwise System.Text.Json defaults
    /// apply. Wraps I/O and <see cref="JsonException"/> into <see cref="CakeException"/> so the
    /// build-host fail surface stays Cake-typed.
    /// </summary>
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

    /// <summary>
    /// Cake-native asynchronous UTF-8 text read. Thin wrapper over
    /// <see cref="IFileSystem.GetFile(FilePath)"/> + <see cref="StreamReader.ReadToEndAsync()"/>
    /// that keeps reads off raw <c>System.IO</c> (preserves testability against
    /// <see cref="Cake.Testing.FakeFileSystem"/>) and surfaces missing-file as a
    /// <see cref="CakeException"/>. Used for non-JSON text artifacts (harvest status files,
    /// Cobertura XML fragments, MSBuild props generation).
    /// </summary>
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

    /// <summary>
    /// Cake-native asynchronous UTF-8 text write. Creates the target directory via
    /// <see cref="DirectoryExtensions.EnsureDirectoryExists"/>, opens a fresh stream with
    /// <c>FileMode.Create</c> (overwrites existing content), and writes <paramref name="content"/>
    /// verbatim. For JSON serialization prefer <see cref="WriteJsonAsync{TModel}"/>; this helper
    /// stays for arbitrary strings (MSBuild props, pre-formatted reports, smoke license
    /// manifests, etc.).
    /// </summary>
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

    /// <summary>
    /// Cake-native serialize + write. Mirror of <see cref="ToJsonAsync{TModel}"/> for the
    /// write direction: serialize <paramref name="model"/> to JSON and persist at
    /// <paramref name="filePath"/> via the Cake <see cref="IFileSystem"/>. Callers pass their
    /// own <see cref="JsonSerializerOptions"/> when they need shaping (indent / enum / naming).
    /// </summary>
    public static async Task WriteJsonAsync<TModel>(
        this ICakeContext cakeContext,
        FilePath filePath,
        TModel model,
        JsonSerializerOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(cakeContext);
        ArgumentNullException.ThrowIfNull(filePath);
        ArgumentNullException.ThrowIfNull(model);

        if (!string.Equals(filePath.GetExtension(), ".json", StringComparison.OrdinalIgnoreCase))
        {
            throw new CakeException($"File extension must be '.json': {filePath.FullPath}");
        }

        cakeContext.EnsureDirectoryExists(filePath.GetDirectory());

        try
        {
            var file = cakeContext.FileSystem.GetFile(filePath);
            await using var stream = file.Open(FileMode.Create, FileAccess.Write, FileShare.None);
            await JsonSerializer.SerializeAsync(stream, model, options ?? DefaultJsonOptions);
        }
        catch (JsonException ex)
        {
            throw new CakeException($"Error serializing JSON to file {filePath.FullPath}: {ex.Message}", ex);
        }
        catch (IOException ex)
        {
            throw new CakeException($"Error writing file {filePath.FullPath}: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Cake-native JSON serialization to string. Complements <see cref="WriteJsonAsync{TModel}"/>
    /// for the in-memory case (logging, CLI argument embedding, inline diagnostics) where no
    /// file is written. Centralizes all <see cref="JsonSerializer.Serialize"/> usage behind the
    /// Cake extension surface so every build-host caller produces JSON with the same default
    /// shape unless it explicitly opts into a domain-specific <see cref="JsonSerializerOptions"/>.
    /// </summary>
#pragma warning disable IDE0060 // Unused `cakeContext` — kept to enforce the Cake-extension convention repo-wide (no naked JsonSerializer calls).
    public static string SerializeJson<TModel>(
        this ICakeContext cakeContext,
        TModel model,
        JsonSerializerOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(cakeContext);
        ArgumentNullException.ThrowIfNull(model);
        return JsonSerializer.Serialize(model, options ?? DefaultJsonOptions);
    }
#pragma warning restore IDE0060

    /// <summary>
    /// Cake-native JSON deserialization from an in-memory UTF-16 string. Non-extension static
    /// helper so Infrastructure readers (VcpkgManifestReader, CoverageBaselineReader, …) that
    /// do not carry an <see cref="ICakeContext"/> dependency can still route their JSON parse
    /// through the repo's central Cake JSON surface. Callers own null-handling and wrap
    /// <see cref="JsonException"/> with domain-specific context.
    /// <para>
    /// AOT / source-gen ready: supply a <see cref="JsonSerializerOptions"/> whose
    /// <c>TypeInfoResolver</c> points at a <c>JsonSerializerContext</c> derived type to bypass
    /// reflection — the helper forwards the options verbatim to
    /// <see cref="JsonSerializer.Deserialize{TValue}(string, JsonSerializerOptions?)"/>.
    /// </para>
    /// </summary>
    public static TModel? DeserializeJson<TModel>(string json, JsonSerializerOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(json);
        return JsonSerializer.Deserialize<TModel>(json, options);
    }

    /// <summary>
    /// Cake-native JSON deserialization from a UTF-8 byte span. Matches
    /// <see cref="DeserializeJson{TModel}(string, JsonSerializerOptions?)"/> for callers that
    /// already hold a UTF-8 buffer (streamed reads, cached CLI output) — avoids the
    /// <c>byte[]</c> → <c>string</c> round-trip while keeping the central-surface convention.
    /// </summary>
    public static TModel? DeserializeJson<TModel>(ReadOnlySpan<byte> utf8Json, JsonSerializerOptions? options = null)
    {
        return JsonSerializer.Deserialize<TModel>(utf8Json, options);
    }
}
