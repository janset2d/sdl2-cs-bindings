namespace Janset.SDL2.PostProcess;

internal static class PostProcessSelfTests
{
    public static int Run()
    {
        var failures = new List<string>();
        const string source = """
using System.Runtime.InteropServices;

namespace SDL2;

internal static unsafe partial class SDLNative
{
    [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern int SDL_Init(uint flags);
}
""";

        var rewritten = PostProcessCli.RewriteWithDllImportToLibraryImport(source);
        CheckDllImportRewrite(rewritten, failures);
        CheckLfFileWriting(failures);
        CheckPathHandlingAndDirectoryRewrite(source, failures);
        CheckPlatformDeltaPassthroughLf(failures);
        CheckLibraryImportModeValidation(failures);
        CheckUniformOpaqueFamilyIdentity(failures);
        CheckOpaqueHandleFamilyAwareness(failures);

        if (failures.Count > 0)
        {
            foreach (var failure in failures)
            {
                Console.WriteLine($"self-test: FAIL: {failure}");
            }

            return 1;
        }

        Console.WriteLine("self-test: PASS");
        return 0;
    }

    private static void CheckDllImportRewrite(string rewritten, List<string> failures)
    {
        if (!rewritten.Contains("using System.Runtime.CompilerServices;", StringComparison.Ordinal))
        {
            failures.Add("DllImport rewriter did not add System.Runtime.CompilerServices using");
        }

        if (!rewritten.Contains("[LibraryImport(\"SDL2\")]", StringComparison.Ordinal))
        {
            failures.Add("DllImport rewriter did not replace DllImport with LibraryImport");
        }

        if (!rewritten.Contains("[UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]", StringComparison.Ordinal))
        {
            failures.Add("DllImport rewriter did not emit CallConvCdecl metadata");
        }

        if (!rewritten.Contains("public static partial int SDL_Init(uint flags);", StringComparison.Ordinal))
        {
            failures.Add("DllImport rewriter did not convert extern method to partial method");
        }

        if (rewritten.Contains("DllImport", StringComparison.Ordinal) || rewritten.Contains(" extern ", StringComparison.Ordinal))
        {
            failures.Add("DllImport rewriter left DllImport or extern in the transformed output");
        }
    }

    private static void CheckPathHandlingAndDirectoryRewrite(string source, List<string> failures)
    {
        using var tempRoot = new TemporaryDirectory();
        var inputDir = Path.Combine(tempRoot.Path, "input", "Modern");
        var outputDir = Path.Combine(tempRoot.Path, "output");
        Directory.CreateDirectory(inputDir);
        File.WriteAllText(Path.Combine(inputDir, "SDL.g.cs"), source);

        var argsWithOutput = new[] { "libraryimport", inputDir, outputDir };
        var resolvedOutput = PostProcessCli.ResolveOutputDirectory(argsWithOutput, inputDir);
        if (!string.Equals(resolvedOutput, Path.GetFullPath(outputDir), StringComparison.OrdinalIgnoreCase))
        {
            failures.Add("CLI output directory resolution did not honor the third positional argument");
        }

        var argsWithFlag = new[] { "uniform-opaque", inputDir, "--owner-mode", "owner" };
        var resolvedFlagOutput = PostProcessCli.ResolveOutputDirectory(argsWithFlag, inputDir);
        if (!string.Equals(resolvedFlagOutput, inputDir, StringComparison.OrdinalIgnoreCase))
        {
            failures.Add("CLI output directory resolution treated a flag as an output directory");
        }

        var directoryRewriter = new DllImportToLibraryImportRewriter();
        var processed = 0;
        var transformed = 0;
        PostProcessCli.ProcessDirectory(
            inputDir,
            resolvedOutput,
            directoryRewriter,
            () => directoryRewriter.AnyChanges,
            directoryRewriter.Reset,
            ref processed,
            ref transformed);

        var outputFile = Path.Combine(outputDir, "SDL.g.cs");
        if (processed != 1 || transformed != 1)
        {
            failures.Add($"directory rewrite expected 1 processed / 1 transformed, got {processed} / {transformed}");
        }

        if (!File.Exists(outputFile) || !File.ReadAllText(outputFile).Contains("LibraryImport", StringComparison.Ordinal))
        {
            failures.Add("directory rewrite did not write transformed LibraryImport output to the explicit output directory");
        }

        if (File.Exists(outputFile) && File.ReadAllBytes(outputFile).Contains((byte)'\r'))
        {
            failures.Add("directory rewrite emitted CR bytes in transformed output");
        }
    }

    private static void CheckLfFileWriting(List<string> failures)
    {
        using var tempRoot = new TemporaryDirectory();
        var outputPath = Path.Combine(tempRoot.Path, "lf.txt");

        PostProcessCli.WriteAllTextLf(outputPath, "alpha\r\nbeta\rgamma\n");

        if (File.ReadAllBytes(outputPath).Contains((byte)'\r'))
        {
            failures.Add("WriteAllTextLf emitted CR bytes");
        }
    }

    private static void CheckPlatformDeltaPassthroughLf(List<string> failures)
    {
        using var tempRoot = new TemporaryDirectory();
        var inputDir = Path.Combine(tempRoot.Path, "input", "Generated", "Compat");
        var outputDir = Path.Combine(tempRoot.Path, "output", "Generated", "Compat");
        Directory.CreateDirectory(inputDir);
        File.WriteAllText(
            Path.Combine(inputDir, "SDL_neutral.g.cs"),
            "namespace SDL2\r\n{\r\n    internal static partial class SDLNative { }\r\n}\r\n");

        new PlatformDeltaPostProcessor().Process(inputDir, outputDir);

        var outputFile = Path.Combine(outputDir, "SDL_neutral.g.cs");
        if (!File.Exists(outputFile))
        {
            failures.Add("platform-delta passthrough did not copy neutral output to explicit output directory");
            return;
        }

        if (File.ReadAllBytes(outputFile).Contains((byte)'\r'))
        {
            failures.Add("platform-delta passthrough emitted CR bytes in copied output");
        }
    }

    private static void CheckLibraryImportModeValidation(List<string> failures)
    {
        using var tempRoot = new TemporaryDirectory();
        var compatDir = Path.Combine(tempRoot.Path, "Generated", "Compat");
        var modernDir = Path.Combine(tempRoot.Path, "Generated", "Modern");
        var nestedModernDir = Path.Combine(tempRoot.Path, "Generated", "Modern", "Nested");
        Directory.CreateDirectory(compatDir);
        Directory.CreateDirectory(modernDir);
        Directory.CreateDirectory(nestedModernDir);

        if (PostProcessCli.ValidateModeInput("libraryimport", compatDir) is null)
        {
            failures.Add("libraryimport mode accepted a Compat input directory");
        }

        if (PostProcessCli.ValidateModeInput("libraryimport", modernDir) is not null)
        {
            failures.Add("libraryimport mode rejected a Modern input directory");
        }

        if (PostProcessCli.ValidateModeInput("libraryimport", nestedModernDir) is null)
        {
            failures.Add("libraryimport mode accepted a nested directory under Generated/Modern");
        }

        if (PostProcessCli.ValidateModeInput("strip-varargs", compatDir) is not null)
        {
            failures.Add("non-libraryimport mode rejected a Compat input directory");
        }
    }

    private static void CheckUniformOpaqueFamilyIdentity(List<string> failures)
    {
        using var tempRoot = new TemporaryDirectory();

        if (UniformOpaqueFamilyIdentity.Resolve(Path.Combine(tempRoot.Path, "scratch"), "SDL2") != "core")
        {
            failures.Add("uniform-opaque family resolver did not default namespace SDL2 to core");
        }

        if (UniformOpaqueFamilyIdentity.Resolve(Path.Combine(tempRoot.Path, "scratch"), "SDL2.Ttf") != "ttf")
        {
            failures.Add("uniform-opaque family resolver did not resolve namespace SDL2.Ttf to ttf");
        }

        var mixerOutput = Path.Combine(tempRoot.Path, "Janset.SDL2.Mixer", "Generated", "Modern");
        if (UniformOpaqueFamilyIdentity.Resolve(mixerOutput, "SDL2") != "mixer")
        {
            failures.Add("uniform-opaque family resolver did not prefer output path family over namespace fallback");
        }
    }

    private static void CheckOpaqueHandleFamilyAwareness(List<string> failures)
    {
        var rosterPath = ResolveRosterPath();

        var (coreAuto, coreForce) = OpaqueHandleEmitRewriter.LoadRoster(rosterPath, "core");
        if (coreAuto.Count != 14)
        {
            failures.Add($"LoadRoster('core') auto-detect count: expected 14, got {coreAuto.Count}");
        }

        if (coreForce.Count != 3)
        {
            failures.Add($"LoadRoster('core') force-opaque count: expected 3, got {coreForce.Count}");
        }

        if (!coreAuto.Contains("SDL_Window"))
        {
            failures.Add("LoadRoster('core') missing SDL_Window");
        }

        if (!coreForce.Contains("SDL_RWops"))
        {
            failures.Add("LoadRoster('core') missing SDL_RWops");
        }

        var (imageAuto, imageForce) = OpaqueHandleEmitRewriter.LoadRoster(rosterPath, "image");
        if (!imageAuto.Contains("SDL_Renderer"))
        {
            failures.Add("LoadRoster('image') cross-family pull missing SDL_Renderer");
        }

        if (!imageForce.Contains("SDL_RWops"))
        {
            failures.Add("LoadRoster('image') cross-family pull missing SDL_RWops");
        }

        var (ttfAuto, ttfForce) = OpaqueHandleEmitRewriter.LoadRoster(rosterPath, "ttf");
        if (!ttfAuto.Contains("TTF_Font"))
        {
            failures.Add("LoadRoster('ttf') missing TTF_Font");
        }

        if (!ttfAuto.Contains("SDL_Renderer"))
        {
            failures.Add("LoadRoster('ttf') cross-family pull missing SDL_Renderer");
        }

        if (!ttfForce.Contains("SDL_RWops"))
        {
            failures.Add("LoadRoster('ttf') cross-family pull missing SDL_RWops");
        }

        var (ttfOwnedAuto, ttfOwnedForce) = OpaqueHandleEmitRewriter.LoadFamilyOwnedRoster(rosterPath, "ttf");
        if (!ttfOwnedAuto.Contains("TTF_Font"))
        {
            failures.Add("LoadFamilyOwnedRoster('ttf') missing TTF_Font");
        }

        if (ttfOwnedAuto.Contains("SDL_Renderer") || ttfOwnedForce.Contains("SDL_RWops"))
        {
            failures.Add("LoadFamilyOwnedRoster('ttf') included pulled Core handles");
        }

        var ttfAppliedHandles = new HashSet<string>(ttfAuto, StringComparer.Ordinal);
        foreach (var handle in ttfForce)
        {
            ttfAppliedHandles.Add(handle);
        }

        var satelliteDriftWarning = CaptureConsoleError(() =>
            OpaqueHandleEmitRewriter.ReportDrift(
                new HashSet<string>(StringComparer.Ordinal) { "TTF_Font", "SDL_RWops" },
                ttfOwnedAuto,
                ttfAppliedHandles,
                "ttf"));

        if (!string.IsNullOrWhiteSpace(satelliteDriftWarning))
        {
            failures.Add($"ReportDrift warned for satellite pulled handles: {satelliteDriftWarning.Trim()}");
        }

        var ttfOwnerHandles = new HashSet<string>(ttfOwnedAuto, StringComparer.Ordinal);
        foreach (var handle in ttfOwnedForce)
        {
            ttfOwnerHandles.Add(handle);
        }

        using (var ownerTempRoot = new TemporaryDirectory())
        {
            var ownerOutputDir = Path.Combine(ownerTempRoot.Path, "Janset.SDL2.Ttf", "Generated", "Modern");
            UniformOpaqueOwnerMode.EmitConsolidatedHandlesFileIfOwner(
                "uniform-opaque",
                ownerOutputDir,
                ttfOwnerHandles,
                isOwner: true,
                namespaceName: "SDL2.Ttf");

            var handlesFile = Path.Combine(ownerOutputDir, "Handles.g.cs");
            var handlesFileContent = File.Exists(handlesFile) ? File.ReadAllText(handlesFile) : string.Empty;
            if (!handlesFileContent.Contains("namespace SDL2.Ttf", StringComparison.Ordinal) ||
                !handlesFileContent.Contains("public readonly partial struct TTF_Font", StringComparison.Ordinal))
            {
                failures.Add("TTF owner-mode Handles.g.cs did not emit TTF_Font in namespace SDL2.Ttf");
            }

            if (handlesFileContent.Contains("SDL_Window", StringComparison.Ordinal) ||
                handlesFileContent.Contains("SDL_RWops", StringComparison.Ordinal))
            {
                failures.Add("TTF owner-mode Handles.g.cs included pulled Core handles");
            }
        }

        var (mixerAuto, _) = OpaqueHandleEmitRewriter.LoadRoster(rosterPath, "mixer");
        if (!mixerAuto.Contains("Mix_Music"))
        {
            failures.Add("LoadRoster('mixer') missing Mix_Music");
        }

        using var tempRoot = new TemporaryDirectory();
        var inputDir = Path.Combine(tempRoot.Path, "Generated", "Modern");
        Directory.CreateDirectory(inputDir);
        File.WriteAllText(Path.Combine(inputDir, "SDL_ttf.g.cs"), """
namespace SDL2.Ttf
{
    public partial struct TTF_Font
    {
    }

    internal static unsafe partial class SDL_ttfNative
    {
        public static partial TTF_Font* TTF_OpenFont(byte* file, int ptsize);
    }
}
""");

        var discovered = OpaqueHandleEmitRewriter.DiscoverAutoDetectedHandles(inputDir);
        if (!discovered.Contains("TTF_Font"))
        {
            failures.Add("DiscoverAutoDetectedHandles missing TTF_Font");
        }

        var handlesContent = OpaqueHandleEmitRewriter.BuildHandlesFileContent(new[] { "TTF_Font" }, "SDL2.Ttf");
        if (!handlesContent.Contains("namespace SDL2.Ttf", StringComparison.Ordinal))
        {
            failures.Add("BuildHandlesFileContent did not emit namespace SDL2.Ttf");
        }

        if (!handlesContent.Contains("public readonly partial struct TTF_Font", StringComparison.Ordinal))
        {
            failures.Add("BuildHandlesFileContent did not emit Pattern B struct for TTF_Font");
        }
    }

    private static string ResolveRosterPath()
    {
        var directory = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (directory is not null)
        {
            var candidate = Path.Combine(
                directory.FullName,
                "spikes",
                "binding-generators",
                "clangsharp",
                "policy",
                "opaque-handle-roster.json");
            if (File.Exists(candidate))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        throw new FileNotFoundException("Could not locate opaque-handle-roster.json by walking ancestors.");
    }

    private static string CaptureConsoleError(Action action)
    {
        var originalError = Console.Error;
        using var writer = new StringWriter();
        Console.SetError(writer);
        try
        {
            action();
            return writer.ToString();
        }
        finally
        {
            Console.SetError(originalError);
        }
    }
}

internal sealed class TemporaryDirectory : IDisposable
{
    public TemporaryDirectory()
    {
        Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "janset-sdl2-postprocess-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path);
    }

    public string Path { get; }

    public void Dispose()
    {
        if (Directory.Exists(Path))
        {
            Directory.Delete(Path, recursive: true);
        }
    }
}
