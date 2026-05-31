using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis;
using Janset.SDL2.PostProcess.Config;

namespace Janset.SDL2.PostProcess;

internal static class PostProcessSelfTests
{
    public static int Run()
    {
        var failures = new List<string>();
        CheckFamilyClassConfig(failures);
        CheckClongAttributeArgumentParsing(failures);
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
        CheckClongParameterDispatch(failures);
        CheckClongSignedReturnDispatch(failures);
        CheckClongThreadIdStructuralDispatch(failures);
        CheckFlagsAttributeDetection(failures);
        CheckStructLayoutSequential(failures);
        CheckPublicTypedMethodProjection(failures);
        CheckPublicProjectionCompiles(failures);

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

    private static void CheckFamilyClassConfig(List<string> failures)
    {
        var config = FamilyConfig.Load();

        if (!string.Equals(config.PublicClass("core"), "SDL", StringComparison.Ordinal))
        {
            failures.Add("FamilyConfig.PublicClass(core) did not return SDL");
        }

        if (!string.Equals(config.RawClass("core"), "SDLNative", StringComparison.Ordinal))
        {
            failures.Add("FamilyConfig.RawClass(core) did not return SDLNative");
        }

        if (!string.Equals(config.PublicClass("gfx"), "SDL2_gfx", StringComparison.Ordinal))
        {
            failures.Add("FamilyConfig.PublicClass(gfx) did not return SDL2_gfx");
        }
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

    private static void CheckClongAttributeArgumentParsing(List<string> failures)
    {
        try
        {
            var libraryImportArgs = SyntaxFactory.ParseAttributeArgumentList("(\"SDL2\", EntryPoint = \"SDL_ThreadID\")")!;
            if (libraryImportArgs.Arguments.Count != 2)
            {
                failures.Add($"ParseAttributeArgumentList(LibraryImport) expected 2 args, got {libraryImportArgs.Arguments.Count}");
            }

            var callConvArgs = SyntaxFactory.ParseAttributeArgumentList("(CallConvs = new[] { typeof(CallConvCdecl) })")!;
            if (callConvArgs.Arguments.Count != 1)
            {
                failures.Add($"ParseAttributeArgumentList(UnmanagedCallConv) expected 1 arg, got {callConvArgs.Arguments.Count}");
            }

            if (!callConvArgs.ToFullString().Contains("typeof(CallConvCdecl)", StringComparison.Ordinal))
            {
                failures.Add($"ParseAttributeArgumentList stripped typeof(CallConvCdecl); got: {callConvArgs.ToFullString()}");
            }
        }
        catch (Exception exc)
        {
            failures.Add($"SyntaxFactory.ParseAttributeArgumentList dry-run threw: {exc.GetType().Name}: {exc.Message}");
        }
    }

    private static void CheckClongParameterDispatch(List<string> failures)
    {
        var clongFixture = """
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace SDL2.Ttf
{
    internal static unsafe partial class SDL_ttfNative
    {
        [DllImport("SDL2_ttf", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern TTF_Font TTF_OpenFontIndex(byte* file, int ptsize, [NativeTypeName("long")] long index);
    }
}
""";

        var clongMethods = new[] { "TTF_OpenFontIndex" };
        var modernRewriter = new ClongDualDispatchRewriter(ClongDualDispatchRewriter.Mode.Modern, clongMethods);
        var modernTree = CSharpSyntaxTree.ParseText(clongFixture);
        var modernRoot = (CompilationUnitSyntax)modernRewriter.Visit(modernTree.GetCompilationUnitRoot())!;
        var modernOutput = modernRoot.ToFullString();
        if (!modernOutput.Contains("CLong index", StringComparison.Ordinal))
        {
            failures.Add("ClongDualDispatchRewriter Modern: did not rewrite `long index` parameter to `CLong index`");
        }

        if (!modernOutput.Contains("[LibraryImport", StringComparison.Ordinal))
        {
            failures.Add("ClongDualDispatchRewriter Modern: did not emit [LibraryImport] attribute");
        }

        var compatRewriter = new ClongDualDispatchRewriter(ClongDualDispatchRewriter.Mode.Compat, clongMethods);
        var compatTree = CSharpSyntaxTree.ParseText(clongFixture);
        var compatRoot = (CompilationUnitSyntax)compatRewriter.Visit(compatTree.GetCompilationUnitRoot())!;
        var compatOutput = compatRoot.ToFullString();
        if (!compatOutput.Contains("RuntimeInformation.IsOSPlatform", StringComparison.Ordinal))
        {
            failures.Add("ClongDualDispatchRewriter Compat: dispatcher missing RuntimeInformation.IsOSPlatform branch");
        }

        if (!compatOutput.Contains("_Win32", StringComparison.Ordinal))
        {
            failures.Add("ClongDualDispatchRewriter Compat: Win32 helper DllImport not emitted");
        }

        if (!compatOutput.Contains("_Unix64", StringComparison.Ordinal))
        {
            failures.Add("ClongDualDispatchRewriter Compat: Unix64 helper DllImport not emitted");
        }

        if (!compatOutput.Contains("(int)index", StringComparison.Ordinal))
        {
            failures.Add("ClongDualDispatchRewriter Compat: Win32 dispatcher did not cast `long index` to `int`");
        }

        if (!compatOutput.Contains("(nint)index", StringComparison.Ordinal))
        {
            failures.Add("ClongDualDispatchRewriter Compat: Unix64 dispatcher did not cast `long index` to `nint`");
        }
    }

    private static void CheckClongSignedReturnDispatch(List<string> failures)
    {
        var signedReturnFixture = """
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace SDL2.Ttf
{
    internal static unsafe partial class SDL_ttfNative
    {
        [DllImport("SDL2_ttf", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        [return: NativeTypeName("long")]
        public static extern long TTF_FontFaces(TTF_Font font);
    }
}
""";

        var clongMethods = new[] { "TTF_FontFaces" };
        var modernRewriter = new ClongDualDispatchRewriter(ClongDualDispatchRewriter.Mode.Modern, clongMethods);
        var modernTree = CSharpSyntaxTree.ParseText(signedReturnFixture);
        var modernRoot = (CompilationUnitSyntax)modernRewriter.Visit(modernTree.GetCompilationUnitRoot())!;
        var modernOutput = modernRoot.ToFullString();
        if (!modernOutput.Contains("CLong TTF_FontFaces", StringComparison.Ordinal))
        {
            failures.Add("ClongDualDispatchRewriter Modern: did not rewrite signed `long` return to `CLong TTF_FontFaces`");
        }

        if (!modernOutput.Contains("[LibraryImport", StringComparison.Ordinal))
        {
            failures.Add("ClongDualDispatchRewriter Modern: did not emit [LibraryImport] for signed `long` return");
        }

        var compatRewriter = new ClongDualDispatchRewriter(ClongDualDispatchRewriter.Mode.Compat, clongMethods);
        var compatTree = CSharpSyntaxTree.ParseText(signedReturnFixture);
        var compatRoot = (CompilationUnitSyntax)compatRewriter.Visit(compatTree.GetCompilationUnitRoot())!;
        var compatOutput = compatRoot.ToFullString();
        if (!compatOutput.Contains("RuntimeInformation.IsOSPlatform", StringComparison.Ordinal))
        {
            failures.Add("ClongDualDispatchRewriter Compat: signed `long` return dispatcher missing RuntimeInformation.IsOSPlatform branch");
        }

        if (!compatOutput.Contains("_Win32", StringComparison.Ordinal) || !compatOutput.Contains("_Unix64", StringComparison.Ordinal))
        {
            failures.Add("ClongDualDispatchRewriter Compat: signed `long` return helper DllImports not emitted");
        }

        if (!compatOutput.Contains("long TTF_FontFaces", StringComparison.Ordinal))
        {
            failures.Add("ClongDualDispatchRewriter Compat: signed `long` return dispatcher did not use `long TTF_FontFaces`");
        }

        if (!compatOutput.Contains("int TTF_FontFaces_Win32", StringComparison.Ordinal))
        {
            failures.Add("ClongDualDispatchRewriter Compat: signed `long` Win32 helper did not use `int TTF_FontFaces_Win32`");
        }

        if (!compatOutput.Contains("nint TTF_FontFaces_Unix64", StringComparison.Ordinal))
        {
            failures.Add("ClongDualDispatchRewriter Compat: signed `long` Unix64 helper did not use `nint TTF_FontFaces_Unix64`");
        }
    }

    private static void CheckClongThreadIdStructuralDispatch(List<string> failures)
    {
        var threadIdFixture = """
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace SDL2
{
    internal static unsafe partial class SDLNative
    {
        [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        [return: NativeTypeName("SDL_threadID")]
        public static extern ulong SDL_ThreadID();

        [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        [return: NativeTypeName("SDL_threadID")]
        public static extern ulong SDL_GetThreadID(SDL_Thread thread);
    }
}
""";

        var clongMethods = new[] { "SDL_ThreadID", "SDL_GetThreadID" };
        var modernRewriter = new ClongDualDispatchRewriter(ClongDualDispatchRewriter.Mode.Modern, clongMethods);
        var modernTree = CSharpSyntaxTree.ParseText(threadIdFixture);
        var modernRoot = (CompilationUnitSyntax)modernRewriter.Visit(modernTree.GetCompilationUnitRoot())!;
        var modernOutput = modernRoot.ToFullString();
        if (!modernOutput.Contains("CULong SDL_ThreadID", StringComparison.Ordinal))
        {
            failures.Add("ClongDualDispatchRewriter Modern: SDL_ThreadID did not use `CULong SDL_ThreadID`");
        }

        if (!modernOutput.Contains("[LibraryImport(\"SDL2\", EntryPoint = \"SDL_ThreadID\")]", StringComparison.Ordinal))
        {
            failures.Add("ClongDualDispatchRewriter Modern: SDL_ThreadID did not emit expected LibraryImport attribute");
        }

        if (!modernOutput.Contains("[return: NativeTypeName(\"SDL_threadID\")]", StringComparison.Ordinal))
        {
            failures.Add("ClongDualDispatchRewriter Modern: SDL_ThreadID did not preserve return NativeTypeName");
        }

        if (!modernOutput.Contains("\n        [UnmanagedCallConv", StringComparison.Ordinal) ||
            !modernOutput.Contains("\n        public static partial CULong SDL_ThreadID", StringComparison.Ordinal))
        {
            failures.Add("ClongDualDispatchRewriter Modern: rewritten member lines did not preserve class-member indentation");
        }

        var compatRewriter = new ClongDualDispatchRewriter(ClongDualDispatchRewriter.Mode.Compat, clongMethods);
        var compatTree = CSharpSyntaxTree.ParseText(threadIdFixture);
        var compatRoot = (CompilationUnitSyntax)compatRewriter.Visit(compatTree.GetCompilationUnitRoot())!;
        var compatOutput = compatRoot.ToFullString();
        if (!compatOutput.Contains("ulong SDL_ThreadID", StringComparison.Ordinal))
        {
            failures.Add("ClongDualDispatchRewriter Compat: SDL_ThreadID dispatcher did not use `ulong SDL_ThreadID`");
        }

        if (!compatOutput.Contains("private static extern uint SDL_ThreadID_Win32", StringComparison.Ordinal))
        {
            failures.Add("ClongDualDispatchRewriter Compat: SDL_ThreadID Win32 helper did not use private `uint SDL_ThreadID_Win32`");
        }

        if (!compatOutput.Contains("private static extern nint SDL_ThreadID_Unix64", StringComparison.Ordinal))
        {
            failures.Add("ClongDualDispatchRewriter Compat: SDL_ThreadID Unix64 helper did not use private `nint SDL_ThreadID_Unix64`");
        }

        if (!compatOutput.Contains("RuntimeInformation.IsOSPlatform", StringComparison.Ordinal))
        {
            failures.Add("ClongDualDispatchRewriter Compat: SDL_ThreadID dispatcher missing RuntimeInformation.IsOSPlatform branch");
        }

        if (!compatOutput.Contains("\n        public static ulong SDL_ThreadID", StringComparison.Ordinal) ||
            !compatOutput.Contains("\n        private static extern uint SDL_ThreadID_Win32", StringComparison.Ordinal) ||
            !compatOutput.Contains("\n        private static extern nint SDL_ThreadID_Unix64", StringComparison.Ordinal))
        {
            failures.Add("ClongDualDispatchRewriter Compat: rewritten member lines did not preserve class-member indentation");
        }

        if (modernOutput.Contains("();[LibraryImport", StringComparison.Ordinal))
        {
            failures.Add("ClongDualDispatchRewriter Modern: adjacent rewritten methods were concatenated without trivia");
        }

        if (compatOutput.Contains("();[DllImport", StringComparison.Ordinal) ||
            compatOutput.Contains("();[return:", StringComparison.Ordinal) ||
            compatOutput.Contains("}[DllImport", StringComparison.Ordinal))
        {
            failures.Add("ClongDualDispatchRewriter Compat: rewritten members were concatenated without trivia");
        }

        if (!modernOutput.Contains("public static partial CULong SDL_ThreadID();\n\n        [LibraryImport(\"SDL2\", EntryPoint = \"SDL_GetThreadID\")]", StringComparison.Ordinal))
        {
            failures.Add("ClongDualDispatchRewriter Modern: replacement group did not preserve blank line before next rewritten method");
        }

        if (!compatOutput.Contains("private static extern nint SDL_ThreadID_Unix64();\n\n        [return: NativeTypeName(\"SDL_threadID\")]", StringComparison.Ordinal))
        {
            failures.Add("ClongDualDispatchRewriter Compat: replacement group did not preserve blank line before next rewritten method");
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

        var config = FamilyConfig.Load();
        new PlatformDeltaPostProcessor(config.PlatformViews()).Process(inputDir, outputDir);

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
        var config = FamilyConfig.Load();

        if (UniformOpaqueFamilyIdentity.Resolve(Path.Combine(tempRoot.Path, "scratch"), "SDL2", config) != "core")
        {
            failures.Add("uniform-opaque family resolver did not default namespace SDL2 to core");
        }

        if (UniformOpaqueFamilyIdentity.Resolve(Path.Combine(tempRoot.Path, "scratch"), "SDL2.Ttf", config) != "ttf")
        {
            failures.Add("uniform-opaque family resolver did not resolve namespace SDL2.Ttf to ttf");
        }

        var mixerOutput = Path.Combine(tempRoot.Path, "Janset.SDL2.Mixer", "Generated", "Modern");
        if (UniformOpaqueFamilyIdentity.Resolve(mixerOutput, "SDL2", config) != "mixer")
        {
            failures.Add("uniform-opaque family resolver did not prefer output path family over namespace fallback");
        }
    }

    private static void CheckOpaqueHandleFamilyAwareness(List<string> failures)
    {
        var config = FamilyConfig.Load();

        var (coreAuto, coreForce) = config.OpaqueHandles("core", includeCoreHandles: true);
        if (coreAuto.Count != 14)
        {
            failures.Add($"OpaqueHandles('core') auto-detect count: expected 14, got {coreAuto.Count}");
        }

        if (coreForce.Count != 3)
        {
            failures.Add($"OpaqueHandles('core') force-opaque count: expected 3, got {coreForce.Count}");
        }

        if (!coreAuto.Contains("SDL_Window"))
        {
            failures.Add("OpaqueHandles('core') missing SDL_Window");
        }

        if (!coreForce.Contains("SDL_RWops"))
        {
            failures.Add("OpaqueHandles('core') missing SDL_RWops");
        }

        var (imageAuto, imageForce) = config.OpaqueHandles("image", includeCoreHandles: true);
        if (!imageAuto.Contains("SDL_Renderer"))
        {
            failures.Add("OpaqueHandles('image') cross-family pull missing SDL_Renderer");
        }

        if (!imageForce.Contains("SDL_RWops"))
        {
            failures.Add("OpaqueHandles('image') cross-family pull missing SDL_RWops");
        }

        var (ttfAuto, ttfForce) = config.OpaqueHandles("ttf", includeCoreHandles: true);
        if (!ttfAuto.Contains("TTF_Font"))
        {
            failures.Add("OpaqueHandles('ttf') missing TTF_Font");
        }

        if (!ttfAuto.Contains("SDL_Renderer"))
        {
            failures.Add("OpaqueHandles('ttf') cross-family pull missing SDL_Renderer");
        }

        if (!ttfForce.Contains("SDL_RWops"))
        {
            failures.Add("OpaqueHandles('ttf') cross-family pull missing SDL_RWops");
        }

        var (ttfOwnedAuto, ttfOwnedForce) = config.OpaqueHandles("ttf", includeCoreHandles: false);
        if (!ttfOwnedAuto.Contains("TTF_Font"))
        {
            failures.Add("OpaqueHandles('ttf', false) missing TTF_Font");
        }

        if (ttfOwnedAuto.Contains("SDL_Renderer") || ttfOwnedForce.Contains("SDL_RWops"))
        {
            failures.Add("OpaqueHandles('ttf', false) included pulled Core handles");
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

        var (mixerAuto, _) = config.OpaqueHandles("mixer", includeCoreHandles: true);
        if (!mixerAuto.Contains("Mix_Music"))
        {
            failures.Add("OpaqueHandles('mixer') missing Mix_Music");
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

    private static void CheckFlagsAttributeDetection(List<string> failures)
    {
        var config = FamilyConfig.Load();
        var coreAllowList = config.FlagsAllowList("core");

        AssertFlagsDecoration(
            "IMG_InitFlags",
            "        IMG_INIT_JPG = 0x00000001,\n        IMG_INIT_PNG = 0x00000002,",
            new HashSet<string>(StringComparer.Ordinal),
            expectFlags: true,
            "FlagsAttributeRewriter suffix rule: IMG_InitFlags",
            failures);

        AssertFlagsDecoration(
            "SDL_Keymod",
            "        KMOD_NONE = 0x0000,\n        KMOD_LSHIFT = 0x0001,\n        KMOD_CTRL = KMOD_LCTRL | KMOD_RCTRL,",
            coreAllowList,
            expectFlags: true,
            "FlagsAttributeRewriter allow-list rule: SDL_Keymod",
            failures);

        AssertFlagsDecoration(
            "SDL_bool",
            "        SDL_FALSE = 0,\n        SDL_TRUE = 1,",
            coreAllowList,
            expectFlags: false,
            "FlagsAttributeRewriter blocked enum: SDL_bool",
            failures);

        AssertFlagsDecoration(
            "SDL_HitTestResult",
            "        SDL_HITTEST_NORMAL,\n        SDL_HITTEST_DRAGGABLE,",
            coreAllowList,
            expectFlags: false,
            "FlagsAttributeRewriter blocked enum: SDL_HitTestResult",
            failures);

        var prefixedInput = """
namespace SDL2
{
    [System.Flags]
    public enum SDL_RendererFlags
    {
        SDL_RENDERER_SOFTWARE = 0x1,
    }
}
""";
        var prefixedRewriter = new FlagsAttributeRewriter(coreAllowList);
        var prefixedRoot = (CompilationUnitSyntax)prefixedRewriter.Visit(CSharpSyntaxTree.ParseText(prefixedInput).GetCompilationUnitRoot())!;
        var prefixedOutput = prefixedRoot.ToFullString();
        var flagsCount = System.Text.RegularExpressions.Regex.Matches(prefixedOutput, @"\[(?:System\.)?Flags(?:Attribute)?\]").Count;
        if (flagsCount != 1)
        {
            failures.Add($"FlagsAttributeRewriter idempotence: expected exactly 1 Flags attribute, got {flagsCount}. Output:\n{prefixedOutput}");
        }

        var separatedInput = """
namespace SDL2
{
    public enum PreludeEnum
    {
        Value,
    }

    public enum SDL_TextureModulate
    {
        SDL_TEXTUREMODULATE_NONE = 0x00000000,
        SDL_TEXTUREMODULATE_COLOR = 0x00000001,
        SDL_TEXTUREMODULATE_ALPHA = 0x00000002,
    }
}
""";
        var separatedRewriter = new FlagsAttributeRewriter(coreAllowList);
        var separatedRoot = (CompilationUnitSyntax)separatedRewriter.Visit(CSharpSyntaxTree.ParseText(separatedInput).GetCompilationUnitRoot())!;
        var separatedOutput = separatedRoot.ToFullString();
        if (!separatedOutput.Contains("[System.Flags]\n    public enum SDL_TextureModulate", StringComparison.Ordinal))
        {
            failures.Add($"FlagsAttributeRewriter trivia: expected attribute to sit directly above enum declaration. Output:\n{separatedOutput}");
        }

        AssertFlagsDecoration(
            "SDL_someflags",
            "        VALUE_A = 0x1,\n        VALUE_B = 0x2,",
            coreAllowList,
            expectFlags: false,
            "FlagsAttributeRewriter suffix case-sensitivity: lowercase flags",
            failures);

        var imageAllowList = config.FlagsAllowList("image");
        if (imageAllowList.Count != 0)
        {
            failures.Add($"config image allow-list: expected empty, got {imageAllowList.Count} entries");
        }

        AssertFlagsDecoration(
            "IMG_InitFlags",
            "        IMG_INIT_JPG = 0x00000001,\n        IMG_INIT_PNG = 0x00000002,",
            imageAllowList,
            expectFlags: true,
            "FlagsAttributeRewriter image family: empty allow-list + IMG_InitFlags suffix",
            failures);
    }

    private static void CheckStructLayoutSequential(List<string> failures)
    {
        var barePodOutput = RewriteStructLayout("""
namespace SDL2
{
    public partial struct SDL_Rect
    {
        public int x;
        public int y;
        public int w;
        public int h;
    }
}
""");

        if (!barePodOutput.Contains("using System.Runtime.InteropServices;", StringComparison.Ordinal))
        {
            failures.Add("StructLayoutSequentialRewriter: bare POD struct did not add System.Runtime.InteropServices using");
        }

        if (!barePodOutput.Contains("    [StructLayout(LayoutKind.Sequential)]\n    public partial struct SDL_Rect", StringComparison.Ordinal))
        {
            failures.Add($"StructLayoutSequentialRewriter: bare POD struct did not add sequential layout with class-member indentation. Output:\n{barePodOutput}");
        }

        var explicitOutput = RewriteStructLayout("""
using System.Runtime.InteropServices;

namespace SDL2
{
    [StructLayout(LayoutKind.Explicit)]
    public partial struct SDL_Event
    {
    }
}
""");

        var explicitLayoutKindCount = CountOccurrences(explicitOutput, "LayoutKind.");
        if (explicitLayoutKindCount != 1 || explicitOutput.Contains("LayoutKind.Sequential", StringComparison.Ordinal))
        {
            failures.Add($"StructLayoutSequentialRewriter: explicit-layout struct was modified. LayoutKind count={explicitLayoutKindCount}. Output:\n{explicitOutput}");
        }

        var existingSequentialOutput = RewriteStructLayout("""
using System.Runtime.InteropServices;

namespace SDL2
{
    [StructLayout(LayoutKind.Sequential)]
    public readonly partial struct SDL_Window
    {
        private readonly nint _handle;
    }
}
""");

        var existingSequentialCount = CountOccurrences(existingSequentialOutput, "StructLayout(LayoutKind.Sequential)");
        if (existingSequentialCount != 1)
        {
            failures.Add($"StructLayoutSequentialRewriter: existing sequential readonly partial struct was double-decorated. Count={existingSequentialCount}. Output:\n{existingSequentialOutput}");
        }

        var partialGuardOutput = RewriteStructLayout("""
using System.Runtime.InteropServices;

namespace SDL2
{
    [StructLayout(LayoutKind.Explicit)]
    public partial struct SDL_Partial
    {
    }

    public partial struct SDL_Partial
    {
        public int Value;
    }
}
""");

        var partialGuardLayoutKindCount = CountOccurrences(partialGuardOutput, "LayoutKind.");
        if (partialGuardLayoutKindCount != 1 || partialGuardOutput.Contains("LayoutKind.Sequential", StringComparison.Ordinal))
        {
            failures.Add($"StructLayoutSequentialRewriter: partial-type guard added a conflicting layout. LayoutKind count={partialGuardLayoutKindCount}. Output:\n{partialGuardOutput}");
        }

        var nestedBareOutput = RewriteStructLayout("""
namespace SDL2
{
    public partial struct SDL_Outer
    {
        public partial struct SDL_Inner
        {
            public int Value;
        }
    }
}
""");

        var nestedBareSequentialCount = CountOccurrences(nestedBareOutput, "StructLayout(LayoutKind.Sequential)");
        if (nestedBareSequentialCount != 2)
        {
            failures.Add($"StructLayoutSequentialRewriter: bare containing struct and nested bare struct were not both decorated. Count={nestedBareSequentialCount}. Output:\n{nestedBareOutput}");
        }

        var explicitOuterNestedBareOutput = RewriteStructLayout("""
using System.Runtime.InteropServices;

namespace SDL2
{
    [StructLayout(LayoutKind.Explicit)]
    public partial struct SDL_ExplicitOuter
    {
        public partial struct SDL_Inner
        {
            public int Value;
        }
    }
}
""");

        var explicitOuterLayoutKindCount = CountOccurrences(explicitOuterNestedBareOutput, "LayoutKind.");
        if (explicitOuterLayoutKindCount != 2 || !explicitOuterNestedBareOutput.Contains("LayoutKind.Explicit", StringComparison.Ordinal) || !explicitOuterNestedBareOutput.Contains("LayoutKind.Sequential", StringComparison.Ordinal))
        {
            failures.Add($"StructLayoutSequentialRewriter: explicit containing struct blocked nested bare struct decoration. LayoutKind count={explicitOuterLayoutKindCount}. Output:\n{explicitOuterNestedBareOutput}");
        }

        var sameSimpleNameNestedOutput = RewriteStructLayout("""
using System.Runtime.InteropServices;

namespace SDL2
{
    [StructLayout(LayoutKind.Explicit)]
    public partial struct SDL_SameName
    {
    }

    public partial struct SDL_Container
    {
        public partial struct SDL_SameName
        {
            public int Value;
        }
    }
}
""");

        var sameSimpleNameLayoutKindCount = CountOccurrences(sameSimpleNameNestedOutput, "LayoutKind.");
        if (sameSimpleNameLayoutKindCount != 3 || CountOccurrences(sameSimpleNameNestedOutput, "LayoutKind.Sequential") != 2)
        {
            failures.Add($"StructLayoutSequentialRewriter: same simple-name guard suppressed unrelated nested struct. LayoutKind count={sameSimpleNameLayoutKindCount}. Output:\n{sameSimpleNameNestedOutput}");
        }
    }

    private static string RewriteStructLayout(string source)
    {
        var rewriter = new StructLayoutSequentialRewriter();
        var root = CSharpSyntaxTree.ParseText(source).GetCompilationUnitRoot();
        return ((CompilationUnitSyntax)rewriter.Visit(root)!).ToFullString();
    }

    private static void CheckPublicTypedMethodProjection(List<string> failures)
    {
        var raw = """
using System;
using System.Runtime.InteropServices;
#if NET5_0_OR_GREATER
using System.Runtime.Versioning;
#endif

namespace SDL2;

public enum IgnoredEnum { Value }
public delegate void IgnoredDelegate();
public readonly partial struct SDL_Surface { }
public readonly partial struct SDL_Renderer { }
public readonly partial struct SDL_Event { }
public readonly partial struct SDL_AssertData { }
public enum SDL_bool { SDL_FALSE = 0, SDL_TRUE = 1 }
public enum SDL_AssertState { SDL_ASSERTION_IGNORE = 0 }
public readonly partial struct TTF_Font { }
public readonly record struct CLong(nint Value);
public readonly record struct CULong(nuint Value);

internal static unsafe partial class NotTheRawClass
{
    public static int SDL_ShouldNotAppear() => 0;
}

internal static unsafe partial class SDLNative
{
    [NativeTypeName("#define SDL_ALPHA_OPAQUE 255")]
    public const int SDL_ALPHA_OPAQUE = 255;
    public const int SDL_BYTEORDER = 1234;
    public const int SDL_FLOATWORDORDER = 4321;
    public const ushort AUDIO_U16SYS = 0x8010;
    public const short AUDIO_S16SYS = unchecked((short)0x9010);
    public const int AUDIO_S32SYS = unchecked((int)0x9020);
    public const uint AUDIO_F32SYS = 0x8120;
    public static ReadOnlySpan<byte> SDL_HINT_RENDER_SCALE_QUALITY => "SDL_RENDER_SCALE_QUALITY"u8;

    public static byte* SDL_GetError() => null;
    public static int SDL_LockSurface(SDL_Surface* surface) => 0;
    public static int SDL_Apple() => 0;
    public static int SDL_Zebra() => 0;
    public static CLong TTF_FontFaces(TTF_Font font) => default;
    public static CULong SDL_ThreadID() => default;
    public static int TTF_OpenFontIndex(TTF_Font font, CLong index, CULong value) => 0;
#if NET5_0_OR_GREATER
    [SupportedOSPlatform("android")]
#endif
    [Obsolete("use something else")]
    public static void SDL_PlatformOnly() { }
    public static void SDL_SetEventFilter(delegate* unmanaged[Cdecl]<nint, SDL_Event*, int> filter, nint userdata) { }
    public static SDL_bool SDL_GetEventFilter(delegate* unmanaged[Cdecl]<nint, SDL_Event*, int>* filter, nint* userdata) => default;
    public static delegate* unmanaged[Cdecl]<SDL_AssertData*, void*, SDL_AssertState> SDL_GetDefaultAssertionHandler() => null;
}
""";

        var members = PublicTypedMethodProjection.DiscoverFromSource(raw, "SDLNative");
        var output = PublicTypedMethodProjection.BuildPublicClassContent(
            members,
            publicClass: "SDL",
            rawClass: "SDLNative",
            namespaceName: "SDL2",
            clongMethods: new HashSet<string>(StringComparer.Ordinal)
            {
                "TTF_FontFaces",
                "SDL_ThreadID",
                "TTF_OpenFontIndex",
            });

        AssertContains(output, "public static unsafe partial class SDL", "PublicTypedMethodProjection: public class shape missing", failures);
        AssertContains(output, "public static byte* SDL_GetError() => SDLNative.SDL_GetError();", "PublicTypedMethodProjection: plain pointer-return forwarder missing", failures);
        AssertContains(output, "public static int SDL_LockSurface(SDL_Surface* surface) => SDLNative.SDL_LockSurface(surface);", "PublicTypedMethodProjection: plain pointer-parameter forwarder missing", failures);
        AssertNotContains(output, "NativeTypeName", "PublicTypedMethodProjection: NativeTypeName leaked", failures);

        AssertContains(output, "public static long TTF_FontFaces(TTF_Font font)", "PublicTypedMethodProjection: signed clong return signature missing", failures);
        AssertContains(output, "#if NET6_0_OR_GREATER", "PublicTypedMethodProjection: clong/callback branch missing", failures);
        AssertContains(output, "return (long)SDLNative.TTF_FontFaces(font).Value;", "PublicTypedMethodProjection: signed clong return Value conversion missing", failures);
        AssertContains(output, "public static ulong SDL_ThreadID()", "PublicTypedMethodProjection: unsigned clong return signature missing", failures);
        AssertContains(output, "return (ulong)SDLNative.SDL_ThreadID().Value;", "PublicTypedMethodProjection: unsigned clong return Value conversion missing", failures);
        AssertContains(output, "TTF_OpenFontIndex(TTF_Font font, long index, ulong value)", "PublicTypedMethodProjection: clong input public signature missing", failures);
        AssertContains(output, "ThrowIfWindowsOutOfRange(index);", "PublicTypedMethodProjection: signed clong Windows range guard missing", failures);
        AssertContains(output, "ThrowIfWindowsOutOfRange(value);", "PublicTypedMethodProjection: unsigned clong Windows range guard missing", failures);
        AssertContains(output, "new CLong((nint)index)", "PublicTypedMethodProjection: signed clong Modern conversion missing", failures);
        AssertContains(output, "new CULong((nuint)value)", "PublicTypedMethodProjection: unsigned clong Modern conversion missing", failures);
        AssertNotContains(output, "(int)index", "PublicTypedMethodProjection: signed clong input truncates through int", failures);
        AssertContains(output, "value > uint.MaxValue", "PublicTypedMethodProjection: unsigned clong range helper missing uint.MaxValue", failures);

        AssertContains(output, "#if NET5_0_OR_GREATER\n    [SupportedOSPlatform(\"android\")]\n#endif", "PublicTypedMethodProjection: guarded platform attribute was not preserved", failures);
        AssertContains(output, "#if NET5_0_OR_GREATER\nusing System.Runtime.Versioning;\n#endif", "PublicTypedMethodProjection: guarded Versioning using missing", failures);
        AssertContains(output, "[Obsolete(\"use something else\")]", "PublicTypedMethodProjection: Obsolete attribute missing", failures);

        AssertContains(output, "public static void SDL_SetEventFilter(nint filter, nint userdata)", "PublicTypedMethodProjection: direct callback public signature missing", failures);
        AssertContains(output, "(delegate* unmanaged[Cdecl]<nint, SDL_Event*, int>)filter", "PublicTypedMethodProjection: direct callback Modern cast missing", failures);
        AssertSignatureNotContains(output, "SDL_SetEventFilter", "delegate*", failures);
        AssertContains(output, "public static SDL_bool SDL_GetEventFilter(nint* filter, nint* userdata)", "PublicTypedMethodProjection: pointer-to-function-pointer public signature missing", failures);
        AssertContains(output, "(delegate* unmanaged[Cdecl]<nint, SDL_Event*, int>*)filter", "PublicTypedMethodProjection: pointer-to-function-pointer Modern cast missing", failures);
        AssertContains(output, "return SDLNative.SDL_GetEventFilter(filter, userdata);", "PublicTypedMethodProjection: callback Compat branch direct forward missing", failures);
        AssertContains(output, "public static nint SDL_GetDefaultAssertionHandler()", "PublicTypedMethodProjection: callback return public signature missing", failures);
        AssertContains(output, "return (nint)SDLNative.SDL_GetDefaultAssertionHandler();", "PublicTypedMethodProjection: callback return Modern cast missing", failures);
        AssertLegacyBranchNotContains(output, "SDL_GetDefaultAssertionHandler", "delegate*", failures);

        AssertContains(output, "public const int SDL_ALPHA_OPAQUE = 255;", "PublicTypedMethodProjection: public const re-emit missing", failures);
        AssertContains(output, "public static ReadOnlySpan<byte> SDL_HINT_RENDER_SCALE_QUALITY => \"SDL_RENDER_SCALE_QUALITY\"u8;", "PublicTypedMethodProjection: string-like macro property re-emit missing", failures);
        AssertNotContains(output, "SDL_BYTEORDER", "PublicTypedMethodProjection: SDL_BYTEORDER should be suppressed", failures);
        AssertNotContains(output, "SDL_FLOATWORDORDER", "PublicTypedMethodProjection: SDL_FLOATWORDORDER should be suppressed", failures);
        AssertNotContains(output, "AUDIO_U16SYS", "PublicTypedMethodProjection: AUDIO_U16SYS should be suppressed", failures);
        AssertNotContains(output, "AUDIO_S16SYS", "PublicTypedMethodProjection: AUDIO_S16SYS should be suppressed", failures);
        AssertNotContains(output, "AUDIO_S32SYS", "PublicTypedMethodProjection: AUDIO_S32SYS should be suppressed", failures);
        AssertNotContains(output, "AUDIO_F32SYS", "PublicTypedMethodProjection: AUDIO_F32SYS should be suppressed", failures);
        AssertNotContains(output, "IgnoredEnum", "PublicTypedMethodProjection: namespace enum leaked", failures);
        AssertNotContains(output, "IgnoredDelegate", "PublicTypedMethodProjection: namespace delegate leaked", failures);
        AssertNotContains(output, "SDL_ShouldNotAppear", "PublicTypedMethodProjection: non-configured raw class leaked", failures);

        var alphaIndex = output.IndexOf("SDL_ALPHA_OPAQUE", StringComparison.Ordinal);
        var hintIndex = output.IndexOf("SDL_HINT_RENDER_SCALE_QUALITY", StringComparison.Ordinal);
        var appleIndex = output.IndexOf("SDL_Apple", StringComparison.Ordinal);
        var zebraIndex = output.IndexOf("SDL_Zebra", StringComparison.Ordinal);
        if (!(alphaIndex >= 0 && hintIndex > alphaIndex && appleIndex > hintIndex && zebraIndex > appleIndex))
        {
            failures.Add($"PublicTypedMethodProjection: deterministic const/property/method ordering failed. Output:\n{output}");
        }

        var emptyOutput = PublicTypedMethodProjection.BuildPublicClassContent(
            PublicTypedMethodProjection.DiscoverFromSource("namespace SDL2; internal static unsafe partial class SDLNative { }", "SDLNative"),
            "SDL",
            "SDLNative",
            "SDL2",
            new HashSet<string>(StringComparer.Ordinal));
        AssertContains(emptyOutput, "public static unsafe partial class SDL", "PublicTypedMethodProjection: empty raw class did not emit public class", failures);

        var satelliteRaw = """
namespace SDL2.Image
{

internal static unsafe partial class SDL_imageNative
{
    public static int IMG_UpdateTexture(SDL_Renderer renderer, SDL_Surface* surface) => 0;
}
}
""";
        var satellitePublic = PublicTypedMethodProjection.BuildPublicClassContent(
            PublicTypedMethodProjection.DiscoverFromSource(satelliteRaw, "SDL_imageNative"),
            "SDL_image",
            "SDL_imageNative",
            "SDL2.Image",
            new HashSet<string>(StringComparer.Ordinal));
        AssertNotContains(satellitePublic, "using SDL2;", "PublicTypedMethodProjection: satellite projection should not add using SDL2", failures);
        AssertCompiles(
            "Satellite public projection resolves Core-owned types through parent namespace",
            "namespace SDL2 { public readonly partial struct SDL_Renderer { } public readonly partial struct SDL_Surface { } }\n// <auto-generated-split />\n" + satelliteRaw + "\n" + satellitePublic,
            defineNet6OrGreater: true,
            failures);
    }

    private static void CheckPublicProjectionCompiles(List<string> failures)
    {
        var rawModern = BuildModernPublicProjectionRawStub();
        var publicText = PublicTypedMethodProjection.BuildPublicClassContent(
            PublicTypedMethodProjection.DiscoverFromSource(rawModern, "SDLNative"),
            "SDL",
            "SDLNative",
            "SDL2",
            new HashSet<string>(StringComparer.Ordinal)
            {
                "SDL_ThreadID",
                "TTF_FontFaces",
                "TTF_OpenFontIndex",
            });

        AssertCompiles("Public projection Modern-shaped raw stub", rawModern + "\n" + publicText, defineNet6OrGreater: true, failures);
        AssertCompiles("Public projection Compat-shaped raw stub", BuildCompatPublicProjectionRawStub() + "\n" + publicText, defineNet6OrGreater: false, failures);
    }

    private static string BuildModernPublicProjectionRawStub() => """
using System;
using System.Runtime.InteropServices;
#if NET5_0_OR_GREATER
using System.Runtime.Versioning;
#endif

namespace SDL2;

public readonly partial struct SDL_Surface { }
public readonly partial struct SDL_Event { }
public readonly partial struct SDL_AssertData { }
public readonly partial struct TTF_Font { }
public enum SDL_bool { SDL_FALSE = 0, SDL_TRUE = 1 }
public enum SDL_AssertState { SDL_ASSERTION_IGNORE = 0 }
public readonly record struct CLong(nint Value);
public readonly record struct CULong(nuint Value);

internal static unsafe partial class SDLNative
{
    [NativeTypeName("#define SDL_ALPHA_OPAQUE 255")]
    public const int SDL_ALPHA_OPAQUE = 255;
    public static ReadOnlySpan<byte> SDL_HINT_RENDER_SCALE_QUALITY => "SDL_RENDER_SCALE_QUALITY"u8;
    public static byte* SDL_GetError() => null;
    public static int SDL_LockSurface(SDL_Surface* surface) => 0;
    public static CULong SDL_ThreadID() => default;
    public static CLong TTF_FontFaces(TTF_Font font) => default;
    public static int TTF_OpenFontIndex(TTF_Font font, CLong index, CULong value) => 0;
#if NET5_0_OR_GREATER
    [SupportedOSPlatform("android")]
#endif
    [Obsolete("old")]
    public static void SDL_PlatformOnly() { }
    public static void SDL_SetEventFilter(delegate* unmanaged[Cdecl]<nint, SDL_Event*, int> filter, nint userdata) { }
    public static SDL_bool SDL_GetEventFilter(delegate* unmanaged[Cdecl]<nint, SDL_Event*, int>* filter, nint* userdata) => default;
    public static delegate* unmanaged[Cdecl]<SDL_AssertData*, void*, SDL_AssertState> SDL_GetDefaultAssertionHandler() => null;
}

internal sealed class NativeTypeNameAttribute(string value) : Attribute;
""";

    private static string BuildCompatPublicProjectionRawStub() => """
using System;

namespace SDL2;

public readonly partial struct SDL_Surface { }
public readonly partial struct SDL_Event { }
public readonly partial struct SDL_AssertData { }
public readonly partial struct TTF_Font { }
public enum SDL_bool { SDL_FALSE = 0, SDL_TRUE = 1 }
public enum SDL_AssertState { SDL_ASSERTION_IGNORE = 0 }

internal static unsafe partial class SDLNative
{
    public const int SDL_ALPHA_OPAQUE = 255;
    public static ReadOnlySpan<byte> SDL_HINT_RENDER_SCALE_QUALITY => "SDL_RENDER_SCALE_QUALITY"u8;
    public static byte* SDL_GetError() => null;
    public static int SDL_LockSurface(SDL_Surface* surface) => 0;
    public static ulong SDL_ThreadID() => 0;
    public static long TTF_FontFaces(TTF_Font font) => 0;
    public static int TTF_OpenFontIndex(TTF_Font font, long index, ulong value) => 0;
    [Obsolete("old")]
    public static void SDL_PlatformOnly() { }
    public static void SDL_SetEventFilter(nint filter, nint userdata) { }
    public static SDL_bool SDL_GetEventFilter(nint* filter, nint* userdata) => default;
    public static nint SDL_GetDefaultAssertionHandler() => 0;
}
""";

    private static void AssertCompiles(string testName, string source, bool defineNet6OrGreater, List<string> failures)
    {
        var symbols = defineNet6OrGreater
            ? new[] { "NET6_0_OR_GREATER", "NET5_0_OR_GREATER" }
            : Array.Empty<string>();
        var parseOptions = CSharpParseOptions.Default
            .WithLanguageVersion(LanguageVersion.Preview)
            .WithPreprocessorSymbols(symbols);
        var trees = source
            .Split(new[] { "// <auto-generated-split />", "// <auto-generated />" }, StringSplitOptions.None)
            .Where(part => !string.IsNullOrWhiteSpace(part))
            .Select(part => CSharpSyntaxTree.ParseText(part.StartsWith("// <auto-generated", StringComparison.Ordinal) ? part : part, parseOptions))
            .ToArray();
        var references = AppDomain.CurrentDomain.GetAssemblies()
            .Where(a => !a.IsDynamic && !string.IsNullOrEmpty(a.Location))
            .Select(a => MetadataReference.CreateFromFile(a.Location))
            .Cast<MetadataReference>()
            .ToArray();
        var compilation = CSharpCompilation.Create(
            "PublicProjectionSelfTest",
            trees,
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, allowUnsafe: true));
        var errors = compilation.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error).ToArray();
        if (errors.Length > 0)
        {
            failures.Add($"{testName} did not compile:\n{string.Join("\n", errors.Select(e => e.ToString()))}\nSource:\n{source}");
        }
    }

    private static void AssertContains(string haystack, string needle, string message, List<string> failures)
    {
        var normalizedHaystack = NormalizeNewlines(haystack);
        var normalizedNeedle = NormalizeNewlines(needle);
        if (!normalizedHaystack.Contains(normalizedNeedle, StringComparison.Ordinal))
        {
            failures.Add($"{message}. Missing `{needle}`. Output:\n{haystack}");
        }
    }

    private static void AssertNotContains(string haystack, string needle, string message, List<string> failures)
    {
        if (NormalizeNewlines(haystack).Contains(NormalizeNewlines(needle), StringComparison.Ordinal))
        {
            failures.Add($"{message}. Unexpected `{needle}`. Output:\n{haystack}");
        }
    }

    private static string NormalizeNewlines(string value)
        => value.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n');

    private static void AssertSignatureNotContains(string output, string methodName, string unexpected, List<string> failures)
    {
        var signatureStart = output.IndexOf(methodName + "(", StringComparison.Ordinal);
        if (signatureStart < 0)
        {
            failures.Add($"PublicTypedMethodProjection: could not find signature for {methodName}. Output:\n{output}");
            return;
        }

        var signatureEnd = output.IndexOf(')', signatureStart);
        var signature = signatureEnd >= 0 ? output[signatureStart..(signatureEnd + 1)] : output[signatureStart..];
        if (signature.Contains(unexpected, StringComparison.Ordinal))
        {
            failures.Add($"PublicTypedMethodProjection: public signature for {methodName} unexpectedly contains `{unexpected}`. Signature: {signature}");
        }
    }

    private static void AssertLegacyBranchNotContains(string output, string methodName, string unexpected, List<string> failures)
    {
        var methodStart = output.IndexOf(methodName + "(", StringComparison.Ordinal);
        if (methodStart < 0)
        {
            failures.Add($"PublicTypedMethodProjection: could not find body for {methodName}. Output:\n{output}");
            return;
        }

        var elseStart = output.IndexOf("#else", methodStart, StringComparison.Ordinal);
        var endifStart = elseStart >= 0 ? output.IndexOf("#endif", elseStart, StringComparison.Ordinal) : -1;
        if (elseStart < 0 || endifStart < 0)
        {
            failures.Add($"PublicTypedMethodProjection: could not find legacy branch for {methodName}. Output:\n{output}");
            return;
        }

        var legacyBranch = output[elseStart..endifStart];
        if (legacyBranch.Contains(unexpected, StringComparison.Ordinal))
        {
            failures.Add($"PublicTypedMethodProjection: legacy branch for {methodName} unexpectedly contains `{unexpected}`. Branch:\n{legacyBranch}");
        }
    }

    private static int CountOccurrences(string haystack, string needle)
    {
        var count = 0;
        var index = 0;
        while ((index = haystack.IndexOf(needle, index, StringComparison.Ordinal)) >= 0)
        {
            count++;
            index += needle.Length;
        }

        return count;
    }

    private static void AssertFlagsDecoration(
        string enumName,
        string members,
        HashSet<string> allowList,
        bool expectFlags,
        string testId,
        List<string> failures)
    {
        var input = $$"""
namespace SDL2
{
    public enum {{enumName}}
    {
{{members}}
    }
}
""";
        var rewriter = new FlagsAttributeRewriter(allowList);
        var root = (CompilationUnitSyntax)rewriter.Visit(CSharpSyntaxTree.ParseText(input).GetCompilationUnitRoot())!;
        var output = root.ToFullString();
        var hasFlags =
            output.Contains("[Flags]", StringComparison.Ordinal) ||
            output.Contains("[FlagsAttribute]", StringComparison.Ordinal) ||
            output.Contains("[System.Flags]", StringComparison.Ordinal) ||
            output.Contains("[System.FlagsAttribute]", StringComparison.Ordinal);
        if (hasFlags != expectFlags)
        {
            failures.Add($"{testId}: expected Flags={expectFlags} on {enumName}, got {hasFlags}. Output:\n{output}");
        }
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
