using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Janset.SDL2.PostProcess;

internal static class PostProcessCli
{
    public static string ResolveOutputDirectory(string[] arguments, string inputDir)
    {
        return arguments.Length >= 3 && !arguments[2].StartsWith("--", StringComparison.Ordinal)
            ? Path.GetFullPath(arguments[2])
            : inputDir;
    }

    public static string? ValidateModeInput(string mode, string inputDir)
    {
        if (mode == "libraryimport" && !IsGeneratedModernDirectory(inputDir))
        {
            return "libraryimport postprocess only supports Generated/Modern input directories; " +
                $"received: {inputDir}";
        }

        return null;
    }

    public static void ProcessDirectory(
        string inputDir,
        string outputDir,
        CSharpSyntaxRewriter rewriter,
        Func<bool> hasChanges,
        Action resetRewriter,
        ref int processed,
        ref int transformed)
    {
        foreach (var file in Directory.EnumerateFiles(inputDir, "*.g.cs", SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(inputDir, file);
            var source = File.ReadAllText(file);
            var root = CSharpSyntaxTree.ParseText(source).GetCompilationUnitRoot();

            resetRewriter();
            var rewritten = (CompilationUnitSyntax)rewriter.Visit(root)!;
            processed++;

            if (!hasChanges())
            {
                if (!string.Equals(inputDir, outputDir, StringComparison.OrdinalIgnoreCase))
                {
                    var passthrough = Path.Combine(outputDir, relative);
                    Directory.CreateDirectory(Path.GetDirectoryName(passthrough)!);
                    File.WriteAllText(passthrough, source);
                }

                continue;
            }

            var destination = Path.Combine(outputDir, relative);
            Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
            File.WriteAllText(destination, rewritten.ToFullString());
            transformed++;
        }
    }

    public static string RewriteWithDllImportToLibraryImport(string source)
    {
        var rewriter = new DllImportToLibraryImportRewriter();
        var root = CSharpSyntaxTree.ParseText(source).GetCompilationUnitRoot();
        rewriter.Reset();
        return ((CompilationUnitSyntax)rewriter.Visit(root)!).ToFullString();
    }

    private static bool IsGeneratedModernDirectory(string path)
    {
        var directory = new DirectoryInfo(Path.GetFullPath(path));
        return string.Equals(directory.Name, "Modern", StringComparison.OrdinalIgnoreCase) &&
            string.Equals(directory.Parent?.Name, "Generated", StringComparison.OrdinalIgnoreCase);
    }
}
