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
        catch
        {
            // Cancellation cannot interrupt rollback; the old roots remain backed up until every replacement succeeds.
            foreach (var output in replaced.AsEnumerable().Reverse())
            {
                if (context.DirectoryExists(output))
                {
                    context.DeleteDirectory(output, new DeleteDirectorySettings { Recursive = true, Force = true });
                }

                if (backups[output] is { } backup)
                {
                    context.CopyDirectory(backup, output);
                }
            }

            throw;
        }
    }
}
