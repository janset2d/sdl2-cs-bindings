using System.Text;
using Cake.Core.IO;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Build.Targets.GenerateBindings.PostProcess;

internal sealed class PlatformDeltaPostProcessor
{
    private readonly string[] _platformOrder;
    private readonly Dictionary<string, string> _supportedOsByPlatform;

    public PlatformDeltaPostProcessor(IReadOnlyList<Config.PlatformView> views)
    {
        _platformOrder = [.. views.Select(v => v.Name)];
        _supportedOsByPlatform = views.ToDictionary(v => v.Name, v => v.SupportedOs, StringComparer.Ordinal);
    }

    public async Task<PlatformDeltaResult> ProcessAsync(GeneratedSyntaxFiles files, DirectoryPath inputDir, DirectoryPath outputDir)
    {
        var generatedRoot = inputDir;
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var processed = 0;
        var transformed = 0;
        var removed = 0;

        foreach (var file in EnumerateNeutralFiles(files, generatedRoot))
        {
            await CollectDeclarationsAsync(files, file, seen);
            await CopyPassthroughFileAsync(files, inputDir, outputDir, file);
        }

        foreach (var file in EnumeratePlatformFiles(files, generatedRoot).Select(file => file.Path))
        {
            processed++;

            var relative = inputDir.GetRelativePath(file);
            var source = await files.ReadAsync(file);
            var platformName = GetPlatformName(relative.FullPath);
            var supportedOs = _supportedOsByPlatform[platformName];

            var tree = CSharpSyntaxTree.ParseText(source, CSharpParseOptions.Default);
            var root = tree.GetCompilationUnitRoot();
            var rewriter = new PlatformDeltaFileRewriter(seen, supportedOs);
            var rewritten = (CompilationUnitSyntax)rewriter.Visit(root);
            var output = rewritten.ToFullString();

            if (rewriter.AddedPlatformAttributes)
            {
                output = EnsureVersioningUsing(output);
                output = GuardPlatformAttributes(output);
            }

            if (rewriter.AnyChanges || !string.Equals(inputDir.FullPath, outputDir.FullPath, StringComparison.OrdinalIgnoreCase))
            {
                var destination = outputDir.CombineWithFilePath(relative);
                await files.WriteAllTextLfAsync(destination, output);
            }

            if (rewriter.AnyChanges)
            {
                transformed++;
                removed += rewriter.RemovedDeclarations;
            }
        }

        return new PlatformDeltaResult(processed, transformed, removed);
    }

    private static async Task CopyPassthroughFileAsync(GeneratedSyntaxFiles files, DirectoryPath inputDir, DirectoryPath outputDir, IFile file)
    {
        if (string.Equals(inputDir.FullPath, outputDir.FullPath, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var relative = inputDir.GetRelativePath(file.Path);
        var destination = outputDir.CombineWithFilePath(relative);
        await files.WriteAllTextLfAsync(destination, await files.ReadAsync(file.Path));
    }

    private static IEnumerable<IFile> EnumerateNeutralFiles(GeneratedSyntaxFiles files, DirectoryPath generatedRoot)
    {
        return files
            .Enumerate(generatedRoot)
            .Where(file => !generatedRoot
                .GetRelativePath(file.Path)
                .FullPath
                .Split('/')
                .Contains("Platforms", StringComparer.Ordinal));
    }

    private IEnumerable<IFile> EnumeratePlatformFiles(GeneratedSyntaxFiles files, DirectoryPath generatedRoot)
    {
        var platformRoot = generatedRoot.Combine("Platforms");

        if (!files.DirectoryExists(platformRoot))
        {
            return [];
        }

        return _platformOrder
            .SelectMany(platform => files
                .Enumerate(platformRoot)
                .Where(file => platformRoot
                    .GetRelativePath(file.Path)
                    .FullPath
                    .Split('/')[0]
                    .Equals(platform, StringComparison.Ordinal))
                .OrderBy(file => file.Path.FullPath, StringComparer.Ordinal));
    }

    private static string GetPlatformName(string relativePath)
    {
        var parts = relativePath.Split('/');

        for (var i = 0; i < parts.Length - 1; i++)
        {
            if (parts[i] == "Platforms")
            {
                return parts[i + 1];
            }
        }

        throw new InvalidOperationException($"Platform file is not under a Platforms/<View> directory: {relativePath}");
    }

    private static async Task CollectDeclarationsAsync(GeneratedSyntaxFiles files, IFile file, ISet<string> seen)
    {
        var root = CSharpSyntaxTree.ParseText(await files.ReadAsync(file.Path), CSharpParseOptions.Default).GetCompilationUnitRoot();
        var collector = new DeclarationCollector(seen);
        collector.Visit(root);
    }

    private static string EnsureVersioningUsing(string source)
    {
        if (source.Contains("System.Runtime.Versioning", StringComparison.Ordinal))
        {
            return source;
        }

        const string marker = "using System.Runtime.InteropServices;\n";
        const string guardedUsing = "using System.Runtime.InteropServices;\n#if NET5_0_OR_GREATER\nusing System.Runtime.Versioning;\n#endif\n";
        return source.Replace(marker, guardedUsing, StringComparison.Ordinal);
    }

    private static string GuardPlatformAttributes(string source)
    {
        var normalized = source.Replace("\r\n", "\n", StringComparison.Ordinal);
        var lines = normalized.TrimEnd('\n').Split('\n');
        var builder = new StringBuilder(source.Length + (lines.Length * 32));

        foreach (var line in lines)
        {
            if (line.Contains("[SupportedOSPlatform(", StringComparison.Ordinal))
            {
                var indentLength = line.Length - line.TrimStart().Length;
                var indent = line[..indentLength];
                builder.Append(indent).AppendLine("#if NET5_0_OR_GREATER");
                builder.AppendLine(line);
                builder.Append(indent).AppendLine("#endif");
                continue;
            }

            builder.AppendLine(line);
        }

        return builder.ToString();
    }
}
