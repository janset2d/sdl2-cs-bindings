using System.Text.Json;
using System.Xml.Linq;
using Build.Data.BindingGeneration.Models;
using Build.Host.Cake;
using Build.Host.Paths;
using Build.Targets.GenerateBindings;
using Build.Targets.GenerateBindings.HeaderSet;
using Cake.Common.IO;
using Cake.Core;
using Cake.Core.IO;

namespace Build.Data.BindingGeneration;

internal sealed class GeneratedStampInputs(ICakeContext context, IPathService paths)
{
    public async Task<GeneratedStamp> CaptureAsync(BindingGenerationConfig family, string triplet, bool shims,
        IReadOnlyList<GeneratedFile> generated, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var manifest = await ReadJsonAsync("build/manifest.json");
        var library = manifest.GetProperty("library_manifests").EnumerateArray().Single(item => item.GetProperty("name").GetString() == family.LibraryName);
        var global = manifest.GetProperty("binding_generation");
        var core = manifest.GetProperty("library_manifests").EnumerateArray().Single(item => item.GetProperty("core_lib").GetBoolean());
        var config = Element(new
        {
            family = family.FamilyId,
            library_name = family.LibraryName,
            library_version = family.LibraryVersion,
            library_port_version = library.GetProperty("vcpkg_port_version"),
            global,
            profile = library.GetProperty("binding_generation"),
            core_opaque_handles = core.GetProperty("binding_generation").GetProperty("opaque_handles"),
        });
        var toolchain = await CaptureToolchainAsync();
        var headers = family.HeaderSet!.Headers.OrderBy(header => header.Order).Select(header => header.Name).ToArray();
        var rspInputs = await InventoryAsync(ResponseFiles(global, family, headers), ct);
        var shimInputs = await InventoryAsync(shims ? Tree(global.GetProperty("platform_header_shims_dir").GetString()!) : [], ct);
        var sourcePaths = new List<string>();

        foreach (var tree in new[] { "build/_build/Targets/GenerateBindings", "build/_build/Tools/ClangSharp", "build/_build/Data/BindingGeneration",
            "build/_build/Data/Manifest", "build/_build/Host" })
        {
            sourcePaths.AddRange(Tree(tree));
        }

        sourcePaths.AddRange(["build/_build/Build.csproj", "build/_build/packages.lock.json", "build/_build/ParsedArguments.cs", "Directory.Build.props"]);
        var sourceInputs = await InventoryAsync(sourcePaths, ct);
        var generatorFingerprint = StampContent.Hash(StampContent.Canonical(context, new { sourceInputs, config, rspInputs, shimInputs, toolchain }));
        var vcpkg = await ReadJsonAsync("vcpkg.json");
        var port = library.GetProperty("vcpkg_name").GetString()!;
        var vcpkgInputs = await InventoryAsync([
            "vcpkg.json", "vcpkg-overlay-triplets/" + triplet + ".cmake", .. Tree("vcpkg-overlay-ports/" + port)
        ], ct);
        var output = new SortedDictionary<string, string>(StringComparer.Ordinal);

        foreach (var file in generated.Where(file => file.RelativePath.EndsWith(".g.cs", StringComparison.Ordinal)))
        {
            output.Add(file.RelativePath.Replace('\\', '/'), StampContent.BlobHash(file.Content));
        }

        return new GeneratedStamp
        {
            Family = family.FamilyId,
            LibraryName = family.LibraryName,
            LibraryVersion = family.LibraryVersion,
            LibraryPortVersion = library.GetProperty("vcpkg_port_version").GetInt32(),
            GeneratorSourceFingerprint = generatorFingerprint,
            GeneratorInputs = sourceInputs,
            Toolchain = toolchain,
            ConfigInputs = config,
            ConfigFingerprint = StampContent.Hash(StampContent.Canonical(context, config)),
            RspInputs = rspInputs,
            ShimInputs = shimInputs,
            VcpkgManifestHash = vcpkgInputs["vcpkg.json"],
            VcpkgBaseline = vcpkg.GetProperty("builtin-baseline").GetString()!,
            VcpkgInputs = vcpkgInputs,
            VcpkgTriplet = triplet,
            UsePlatformHeaderShims = shims,
            CodegenPasses = [.. GenerationPlan.Codegens, .. BindingPostProcess.Invocations([family]).Select(pass => pass.Mode + ":" + pass.Codegen)],
            ParseViews = Element(new { neutral = new { undefines = global.GetProperty("all_platform_macros") }, platforms = global.GetProperty("platform_views") }),
            SelectedHeaders = headers,
            HeaderCount = headers.Length,
            HeaderInputs = new SortedDictionary<string, string>(StringComparer.Ordinal),
            HeaderFingerprint = "",
            HeaderInputCount = 0,
            GeneratedFiles = output,
            GeneratedFileCount = output.Count,
        };
    }

    public async Task<GeneratedStamp?> CaptureHeadersAsync(GeneratedStamp stamp, BindingGenerationConfig family, CancellationToken ct)
    {
        var include = paths.GetVcpkgInstalledTripletDir(stamp.VcpkgTriplet).Combine(family.HeaderSet!.IncludeDir);

        if (!context.DirectoryExists(include))
        {
            return null;
        }

        var missing = stamp.SelectedHeaders.Where(name => !context.FileExists(include.CombineWithFilePath(name))).ToList();

        if (family.FamilyId == "sdl2-core" && !context.FileExists(include.CombineWithFilePath("SDL.h")))
        {
            missing.Add("SDL.h");
        }

        if (missing.Count > 0)
        {
            throw new CakeException("missing selected header: " + string.Join(", ", missing));
        }

        var shimRoot = paths.RepoRoot.Combine(stamp.ConfigInputs.GetProperty("global").GetProperty("platform_header_shims_dir").GetString()!);
        var headerFiles = context.FileSystem.GetDirectory(include).GetFiles("*", SearchScope.Recursive).Select(file => file.Path).ToArray();
        var inputs = new SortedDictionary<string, string>(StringComparer.Ordinal);

        foreach (var file in headerFiles)
        {
            ct.ThrowIfCancellationRequested();
            inputs.Add("include/" + include.GetRelativePath(file).FullPath.Replace('\\', '/'), StampContent.Hash(await context.ReadAllTextAsync(file)));
        }

        foreach (var input in stamp.ShimInputs)
        {
            inputs.Add("shim/" + input.Key, input.Value);
        }

        // The resolver's selected count remains independent of the conservative content inventory.
        var fingerprint = await new HeaderSetFingerprintCalculator(context).ComputeAsync(new ResolvedHeaderSet(include, shimRoot, include, headerFiles),
            stamp.UsePlatformHeaderShims ? null : [], ct);
        return stamp with { HeaderInputs = inputs, HeaderFingerprint = fingerprint.Hash, HeaderInputCount = inputs.Count };
    }

    public async Task<IReadOnlyList<GeneratedFile>> ReadGeneratedAsync(DirectoryPath root, CancellationToken ct)
    {
        var files = new List<GeneratedFile>();

        foreach (var file in context.FileSystem.GetDirectory(root).GetFiles("*.g.cs", SearchScope.Recursive).Select(file => file.Path))
        {
            ct.ThrowIfCancellationRequested();
            files.Add(new GeneratedFile(root.GetRelativePath(file).FullPath, await context.ReadAllTextAsync(file)));
        }

        return files;
    }

    private IEnumerable<string> Tree(string relative)
    {
        var root = paths.RepoRoot.Combine(relative);
        return !context.DirectoryExists(root) ? [] : context.FileSystem
            .GetDirectory(root)
            .GetFiles("*", SearchScope.Recursive)
            .Select(file => paths.RepoRoot.GetRelativePath(file.Path).FullPath.Replace('\\', '/'));
    }

    private async Task<SortedDictionary<string, string>> InventoryAsync(IEnumerable<string> files, CancellationToken ct)
    {
        var result = new SortedDictionary<string, string>(StringComparer.Ordinal);

        foreach (var file in files.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal))
        {
            ct.ThrowIfCancellationRequested();
            result.Add(file, StampContent.Hash(await ReadTextAsync(file)));
        }

        return result;
    }

    private Task<string> ReadTextAsync(string path) => context.ReadAllTextAsync(paths.RepoRoot.CombineWithFilePath(path));

    private IEnumerable<string> ResponseFiles(JsonElement global, BindingGenerationConfig family, string[] headers)
    {
        yield return global.GetProperty("base_rsp").GetString()!;
        yield return family.Rsp;

        foreach (var header in headers)
        {
            var candidate = global.GetProperty("per_header_rsp_dir").GetString() + "/" + new FilePath(header).GetFilenameWithoutExtension().FullPath + ".rsp";

            if (context.FileExists(paths.RepoRoot.CombineWithFilePath(candidate)))
            {
                yield return candidate;
            }
        }
    }

    private async Task<JsonElement> CaptureToolchainAsync()
    {
        var tools = await ReadJsonAsync("dotnet-tools.json");
        var sdk = await ReadJsonAsync("global.json");
        var packages = XDocument.Parse(await ReadTextAsync("Directory.Packages.props"));
        var roslyn = packages.Descendants("PackageVersion").Single(row => (string?)row.Attribute("Include") == "Microsoft.CodeAnalysis.CSharp");
        return Element(new
        {
            dotnet_sdk = sdk.GetProperty("sdk"),
            clangsharp_tool = tools.GetProperty("tools").GetProperty("clangsharppinvokegenerator"),
            roslyn = (string?)roslyn.Attribute("Version"),
            native_runtime = "Not measured; the declared ClangSharp package supplies native dependencies. System SDK headers are not fingerprinted.",
        });
    }

    private async Task<JsonElement> ReadJsonAsync(string path) => CakeJsonExtensions.DeserializeJson<JsonElement>(await ReadTextAsync(path));

    private JsonElement Element<T>(T value) where T : notnull => CakeJsonExtensions.DeserializeJson<JsonElement>(StampContent.Canonical(context, value));
}
