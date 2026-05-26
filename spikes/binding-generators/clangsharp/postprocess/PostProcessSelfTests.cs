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
        CheckPathHandlingAndDirectoryRewrite(source, failures);
        CheckLibraryImportModeValidation(failures);

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
