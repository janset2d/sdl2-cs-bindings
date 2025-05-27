#pragma warning disable CA1031

using Build.Modules.Contracts;
using Build.Modules.Harvesting.Models;
using Build.Modules.Harvesting.Results;
using Cake.Common.IO;
using Cake.Core;
using Cake.Core.Diagnostics;
using Cake.Core.IO;
using Cake.Common;

namespace Build.Modules.Harvesting;

public sealed class ArtifactDeployer(ICakeContext ctx) : IArtifactDeployer
{
    private readonly ICakeContext _ctx = ctx ?? throw new ArgumentNullException(nameof(ctx));
    private readonly ICakeLog _log = ctx.Log;
    private readonly ICakeEnvironment _environment = ctx.Environment;

    public async Task<CopierResult> DeployArtifactsAsync(DeploymentPlan plan, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(plan);

        if (!plan.Actions.Any())
        {
            _log.Verbose("No actions to execute in the deployment plan.");
            return CopierResult.ToSuccess();
        }

        _log.Verbose("Executing deployment plan with {0} action(s)...", plan.Actions.Count);

        foreach (var action in plan.Actions)
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                switch (action)
                {
                    case FileCopyAction fileCopy:
                        await ExecuteFileCopyActionAsync(fileCopy);
                        break;
                    case ArchiveCreationAction archiveCreation:
                        await ExecuteArchiveCreationActionAsync(archiveCreation, ct);
                        break;
                    default:
                        _log.Warning("Unsupported deployment action type: {0}", action.GetType().Name);
                        break;
                }
            }
            catch (OperationCanceledException)
            {
                _log.Warning("Deployment action was canceled.");
                throw;
            }
            catch (Exception ex)
            {
                var message = $"Error executing deployment action {action.GetType().Name}: {ex.Message}";
                _log.Error(message);

                return new CopierError(message, ex);
            }
        }

        _log.Verbose("Successfully executed deployment plan.");
        return CopierResult.ToSuccess();
    }

    private async Task ExecuteFileCopyActionAsync(FileCopyAction action)
    {
        _log.Verbose("Copying file: {0} to {1} (Origin: {2}, Package: {3})",
            action.SourcePath.GetFilename(),
            action.TargetPath,
            action.Origin,
            action.PackageName);
        _ctx.EnsureDirectoryExists(action.TargetPath.GetDirectory());
        _ctx.CopyFile(action.SourcePath, action.TargetPath);
        await Task.Yield(); // Be a good citizen
    }

    private async Task ExecuteArchiveCreationActionAsync(ArchiveCreationAction action, CancellationToken ct)
    {
        _log.Verbose("Creating archive: {0} from {1} item(s) (Base: {2})", action.ArchivePath, action.ItemsToArchive.Count, action.BaseDirectory);

        if (!action.ItemsToArchive.Any())
        {
            _log.Verbose("No items to archive for {0}. Skipping.", action.ArchiveName);
            return;
        }

        _ctx.EnsureDirectoryExists(action.ArchivePath.GetDirectory());

        var fileListPath = await CreateArchiveFileListAsync(action.ItemsToArchive, ct);
        try
        {
            var processSettings = new ProcessSettings
            {
                Arguments = new ProcessArgumentBuilder()
                    .Append("-czf")
                    .AppendQuoted(action.ArchivePath.FullPath)
                    .Append("-T")
                    .AppendQuoted(fileListPath.FullPath),
                WorkingDirectory = action.BaseDirectory,
            };

            _log.Debug("Running tar process for archive {0}...", action.ArchiveName);
            _log.Verbose("  Command: tar");
            _log.Verbose("  Arguments: {0}", processSettings.Arguments.Render());
            _log.Verbose("  Working Directory: {0}", processSettings.WorkingDirectory.FullPath);

            var exitCode = _ctx.StartProcess("tar", processSettings);

            if (exitCode != 0)
            {
                var errorMessage = $"tar command failed with exit code {exitCode} while creating archive {action.ArchivePath.FullPath}.";
                _log.Error(errorMessage);
                throw new CakeException(errorMessage);
            }

            _log.Verbose("Successfully created archive: {0} ({1} items)", action.ArchiveName, action.ItemsToArchive.Count);
        }
        finally
        {
            if (_ctx.FileExists(fileListPath))
            {
                _log.Debug("Deleting temporary file list: {0}", fileListPath.FullPath);
                _ctx.DeleteFile(fileListPath);
            }
        }
    }

    private async Task<FilePath> CreateArchiveFileListAsync(IReadOnlyList<ArchivedItemDetails> itemsToArchive, CancellationToken ct)
    {
        var tempDir = _ctx.Directory(_environment.WorkingDirectory.Combine(".cake/temp/filelists").FullPath);
        _ctx.EnsureDirectoryExists(tempDir);
        var fileListPath = tempDir.Path.CombineWithFilePath($"archive-files-{Guid.NewGuid():N}.txt");

        var absolutePaths = itemsToArchive.Select(item => item.SourcePath.GetFilename().FullPath).ToList();

        await File.WriteAllLinesAsync(fileListPath.FullPath, absolutePaths, ct);
        _log.Verbose("Created file list for tar: {0} ({1} files), using absolute paths",
            fileListPath.GetFilename(),
            absolutePaths.Count);

        return fileListPath;
    }
}
