using System.Globalization;
using Build.Data.BindingGeneration;
using Build.Data.BindingGeneration.Models;
using Cake.Common.IO;
using Cake.Core;
using Cake.Core.IO;

namespace Build.Targets.GenerateBindings;

internal sealed class GeneratedFamilyPublisher(ICakeContext context)
{
    public async Task PublishAsync(IReadOnlyList<GeneratedFamilySnapshot> families, DirectoryPath backupRoot, CancellationToken ct)
    {
        var writer = new GeneratedOutputWriter(context);

        foreach (var family in families)
        {
            writer.Validate(family.Root, family.OutputDirectory, family.Files);
        }

        ct.ThrowIfCancellationRequested();
        var backups = new Dictionary<DirectoryPath, DirectoryPath?>();

        foreach (var output in families.Select(family => family.OutputDirectory))
        {
            var backup = backupRoot.Combine(backups.Count.ToString(CultureInfo.InvariantCulture));
            var exists = context.DirectoryExists(output);
            backups.Add(output, exists ? backup : null);

            if (exists)
            {
                context.CopyDirectory(output, backup);
            }
        }

        var replaced = new List<DirectoryPath>();

        try
        {
            foreach (var family in families)
            {
                ct.ThrowIfCancellationRequested();
                replaced.Add(family.OutputDirectory);
                await writer.WriteAsync(family.Root, family.OutputDirectory, family.Files, ct);
                ct.ThrowIfCancellationRequested();
            }
        }
        catch (Exception error)
        {
            // Rollback ignores the run's cancellation and attempts every root before deciding whether backups are safe to delete.
            var failures = new List<Exception>();

            foreach (var output in replaced.AsEnumerable().Reverse())
            {
                try
                {
                    if (backups[output] is { } backup)
                    {
                        Restore(backup, output);
                    }
                    else
                    {
                        context.EnsureDirectoryDoesNotExist(output);
                    }
                }
                catch (Exception restoreError)
                {
                    failures.Add(new IOException($"Failed to restore '{output.FullPath}' from backup '{backups[output]?.FullPath}': {restoreError.Message}", restoreError));
                }
            }

            if (failures.Count != 0)
            {
                throw new CakeException($"Rollback failed; backups retained at '{backupRoot.FullPath}'.\n{string.Join('\n', failures.Select(failure => failure.Message))}",
                    new AggregateException(failures.Prepend(error)));
            }

            context.EnsureDirectoryDoesNotExist(backupRoot);
            throw;
        }

        context.EnsureDirectoryDoesNotExist(backupRoot);
    }

    private void Restore(DirectoryPath backup, DirectoryPath output)
    {
        // Merge the old bytes first: a failed copy must not discard the replacement or the recovery source.
        context.CopyDirectory(backup, output);
        var directory = context.FileSystem.GetDirectory(output);

        var extraFiles = directory
            .GetFiles("*", SearchScope.Recursive)
            .Where(file => !context.FileExists(backup.CombineWithFilePath(output.GetRelativePath(file.Path))));

        foreach (var file in extraFiles)
        {
            context.DeleteFile(file.Path);
        }

        var extraDirectories = directory
            .GetDirectories("*", SearchScope.Recursive)
            .Where(child => !context.DirectoryExists(backup.Combine(output.GetRelativePath(child.Path))))
            .OrderByDescending(child => child.Path.Segments.Length);

        foreach (var child in extraDirectories)
        {
            context.DeleteDirectory(child.Path, new DeleteDirectorySettings { Recursive = true, Force = true });
        }
    }
}
