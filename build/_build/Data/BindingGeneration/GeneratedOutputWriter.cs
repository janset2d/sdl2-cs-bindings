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
        ArgumentNullException.ThrowIfNull(root);
        ArgumentNullException.ThrowIfNull(outputDirectory);
        ArgumentNullException.ThrowIfNull(files);
        Validate(root, outputDirectory, files);
        ct.ThrowIfCancellationRequested();

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
            var content = file.Content.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n');
            await _context.WriteAllTextAsync(targetPath, content);
        }
    }

    public void Validate(DirectoryPath root, DirectoryPath outputDirectory, IReadOnlyList<GeneratedFile> files)
    {
        ArgumentNullException.ThrowIfNull(root);
        ArgumentNullException.ThrowIfNull(outputDirectory);
        ArgumentNullException.ThrowIfNull(files);
        EnsureSafeGeneratedOutputDirectory(_context.Environment, root, outputDirectory);
        var targets = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var relativePath in files.Select(file => file.RelativePath))
        {
            var path = relativePath.Replace('\\', '/');

            if (string.IsNullOrWhiteSpace(path) || path.StartsWith('/', StringComparison.Ordinal) || path.Contains(':', StringComparison.Ordinal) ||
                path.Split('/').Any(segment => segment is ".." or "." or "") || !targets.Add(path))
            {
                throw new CakeException($"Refusing unsafe or duplicate generated file path: '{relativePath}'.");
            }

            EnsureSafeGeneratedOutputDirectory(_context.Environment, outputDirectory,
                outputDirectory.Combine(path));
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

        if (!candidate.FullPath.StartsWith(root.FullPath.TrimEnd('/') + "/", comparison))
        {
            throw new CakeException($"Refusing to clear generated bindings output outside '{root.FullPath}': '{candidate.FullPath}'.");
        }

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
