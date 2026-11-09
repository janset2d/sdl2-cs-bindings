using Build.Data.BindingGeneration.Models;
using Build.Host.Cake;
using Cake.Common.IO;
using Cake.Core;
using Cake.Core.IO;

namespace Build.Data.BindingGeneration;

public sealed class GeneratedOutputWriter(ICakeContext context)
{
    private readonly ICakeContext _context = context ?? throw new ArgumentNullException(nameof(context));

    public async Task WriteAsync(
        DirectoryPath root,
        DirectoryPath outputDirectory,
        IReadOnlyList<GeneratedFile> files,
        CancellationToken ct = default)
    {
        EnsureSafeGeneratedOutputDirectory(_context.Environment, root, outputDirectory);

        if (_context.DirectoryExists(outputDirectory))
        {
            ClearDirectoryContents(_context, outputDirectory);
        }

        _context.EnsureDirectoryExists(outputDirectory);

        foreach (var file in files)
        {
            ct.ThrowIfCancellationRequested();
            var targetPath = outputDirectory.CombineWithFilePath(file.RelativePath);
            _context.EnsureDirectoryExists(targetPath.GetDirectory());
            await _context.WriteAllTextAsync(targetPath, file.Content).ConfigureAwait(false);
        }
    }

    private static void ClearDirectoryContents(ICakeContext context, DirectoryPath outputDirectory)
    {
        var files = context
            .GetFiles($"{outputDirectory.FullPath}/**/*")
            .Concat(context.GetFiles($"{outputDirectory.FullPath}/*"))
            .DistinctBy(file => file.FullPath, StringComparer.Ordinal);

        foreach (var file in files)
        {
            context.DeleteFile(file);
        }

        var directories = context
            .GetDirectories($"{outputDirectory.FullPath}/**/*")
            .Concat(context.GetDirectories($"{outputDirectory.FullPath}/*"))
            .DistinctBy(directory => directory.FullPath, StringComparer.Ordinal)
            .OrderByDescending(directory => directory.Segments.Length)
            .ThenByDescending(directory => directory.FullPath, StringComparer.Ordinal);

        foreach (var directory in directories)
        {
            context.DeleteDirectory(directory, new DeleteDirectorySettings
            {
                Recursive = true,
                Force = true,
            });
        }
    }

    private static void EnsureSafeGeneratedOutputDirectory(ICakeEnvironment environment, DirectoryPath previewRoot, DirectoryPath outputDirectory)
    {
        var root = previewRoot.MakeAbsolute(environment).Collapse();
        var candidate = outputDirectory.MakeAbsolute(environment).Collapse();
        var comparison = environment.Platform.Family == PlatformFamily.Windows
            ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        var rootSegments = root.Segments;
        var candidateSegments = candidate.Segments;

        if (candidateSegments.Length <= rootSegments.Length)
        {
            throw new CakeException($"Refusing to clear generated bindings output outside '{root.FullPath}': '{candidate.FullPath}'.");
        }

        for (var i = 0; i < rootSegments.Length; i++)
        {
            if (!string.Equals(rootSegments[i], candidateSegments[i], comparison))
            {
                throw new CakeException($"Refusing to clear generated bindings output outside '{root.FullPath}': '{candidate.FullPath}'.");
            }
        }
    }
}
