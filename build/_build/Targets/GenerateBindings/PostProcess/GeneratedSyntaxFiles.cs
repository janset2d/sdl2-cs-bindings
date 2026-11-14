using Build.Host.Cake;
using Cake.Core;
using Cake.Core.IO;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Build.Targets.GenerateBindings.PostProcess;

internal sealed class GeneratedSyntaxFiles(ICakeContext context)
{
    public static string NormalizeLineEndings(string content)
    {
        return content.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n');
    }

    public IEnumerable<IFile> Enumerate(DirectoryPath directory)
    {
        return context.FileSystem.GetDirectory(directory).GetFiles("*.g.cs", SearchScope.Recursive);
    }

    public bool DirectoryExists(DirectoryPath directory) => context.FileSystem.GetDirectory(directory).Exists;

    public Task<string> ReadAsync(FilePath path)
    {
        return context.ReadAllTextAsync(path);
    }

    public Task WriteAllTextLfAsync(FilePath path, string content)
    {
        return context.WriteAllTextAsync(path, NormalizeLineEndings(content));
    }

    public async Task<(int Processed, int Transformed)> RewriteAsync(
        DirectoryPath inputDirectory,
        DirectoryPath outputDirectory,
        CSharpSyntaxRewriter rewriter,
        Func<bool> hasChanges,
        Action resetRewriter)
    {
        if (rewriter is DllImportToLibraryImportRewriter &&
            DllImportToLibraryImportRewriter.ValidateInput(inputDirectory) is { } error)
        {
            throw new InvalidOperationException(error);
        }

        var processed = 0;
        var transformed = 0;

        foreach (var file in Enumerate(inputDirectory).Select(file => file.Path))
        {
            var relative = inputDirectory.GetRelativePath(file);
            var source = await ReadAsync(file);
            var root = CSharpSyntaxTree.ParseText(source, CSharpParseOptions.Default).GetCompilationUnitRoot();
            resetRewriter();
            var rewritten = (CompilationUnitSyntax)rewriter.Visit(root);
            processed++;

            if (!hasChanges())
            {
                if (!string.Equals(inputDirectory.FullPath, outputDirectory.FullPath, StringComparison.OrdinalIgnoreCase))
                {
                    await WriteAllTextLfAsync(outputDirectory.CombineWithFilePath(relative), source);
                }

                continue;
            }

            await WriteAllTextLfAsync(outputDirectory.CombineWithFilePath(relative), rewritten.ToFullString());
            transformed++;
        }

        return (processed, transformed);
    }
}
