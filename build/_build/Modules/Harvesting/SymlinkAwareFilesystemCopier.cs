#pragma warning disable CA1031

using Build.Modules.Contracts;
using Build.Modules.Harvesting.Models;
using Build.Modules.Harvesting.Results;
using Cake.Common.IO;
using Cake.Common;
using Cake.Core;
using Cake.Core.Diagnostics;
using Cake.Core.IO;

namespace Build.Modules.Harvesting;

public sealed class SymlinkAwareFilesystemCopier(ICakeContext ctx, IRuntimeProfile profile) : IFilesystemCopier
{
    private readonly ICakeContext _ctx = ctx ?? throw new ArgumentNullException(nameof(ctx));
    private readonly IRuntimeProfile _profile = profile ?? throw new ArgumentNullException(nameof(profile));
    private readonly ICakeLog _log = ctx.Log;

    public async Task<CopierResult> CopyAsync(IEnumerable<NativeArtifact> artifacts, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(artifacts);

        try
        {
            var artifactList = artifacts.ToList();
            if (artifactList.Count == 0)
            {
                _log.Information("No artifacts to copy");
                return CopierResult.ToSuccess();
            }

            if (_profile.PlatformFamily == PlatformFamily.Windows)
            {
                return await CopyWindowsFilesAsync(artifactList, ct);
            }
            else
            {
                return await CreateUnixArchiveAsync(artifactList, ct);
            }
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            return new CopierError($"Error while processing artifacts: {ex.Message}", ex);
        }
    }

    private async Task<CopierResult> CopyWindowsFilesAsync(List<NativeArtifact> artifacts, CancellationToken ct)
    {
        _log.Information("Copying {0} Windows artifacts individually", artifacts.Count);

        foreach (var artifact in artifacts)
        {
            ct.ThrowIfCancellationRequested();

            _ctx.EnsureDirectoryExists(artifact.TargetPath.GetDirectory());
            _ctx.CopyFile(artifact.SourcePath, artifact.TargetPath);
            _log.Verbose("copied {0} → {1}", artifact.SourcePath.GetFilename(), artifact.TargetPath);

            await Task.Yield();
        }

        _log.Information("Successfully copied {0} Windows artifacts", artifacts.Count);
        return CopierResult.ToSuccess();
    }

    private async Task<CopierResult> CreateUnixArchiveAsync(List<NativeArtifact> artifacts, CancellationToken ct)
    {
        var rid = _profile.Rid;
        var archiveName = $"natives-{rid}.tar.gz"; // As per plan, consider 'payload.archive' or similar generic name if preferred

        _log.Information("Preparing to create Unix archive {0} for RID {1}", archiveName, rid);

        var runtimeOrPrimaryArtifacts = artifacts
            .Where(artifact => artifact.Origin is ArtifactOrigin.Runtime or ArtifactOrigin.Primary)
            .ToList();

        if (runtimeOrPrimaryArtifacts.Count == 0)
        {
            _log.Information("No runtime or primary artifacts to archive for RID {0}", rid);
            return CopierResult.ToSuccess();
        }

        // Determine the base directory for source files and the output directory for the archive.
        // This assumes all runtime/primary artifacts for a given library share a common source parent dir (e.g., vcpkg_installed/.../bin)
        // and a common target parent dir (e.g., HarvestOutput/.../runtimes/rid/native).
        DirectoryPath baseDirForTar = runtimeOrPrimaryArtifacts[0].SourcePath.GetDirectory();
        DirectoryPath archiveOutputDirectory = runtimeOrPrimaryArtifacts[0].TargetPath.GetDirectory();

        _ctx.EnsureDirectoryExists(archiveOutputDirectory);
        var archivePath = archiveOutputDirectory.CombineWithFilePath(archiveName);

        _log.Debug("Base directory for tar sources: {0}", baseDirForTar.FullPath);
        _log.Debug("Output path for tar archive: {0}", archivePath.FullPath);

        // Create a temporary file listing relative paths for tar
        var fileListPath = await CreateFileListAsync(runtimeOrPrimaryArtifacts, baseDirForTar, ct);

        try
        {
            var processSettings = new ProcessSettings
            {
                Arguments = new ProcessArgumentBuilder()
                    .Append("-czf") // Create, gzip, use file
                    .AppendQuoted(archivePath.FullPath) // Output archive file path
                    .Append("-T") // Read file names from file
                    .AppendQuoted(fileListPath.FullPath), // File containing list of input files (relative paths)
                WorkingDirectory = baseDirForTar, // Run tar from the base directory of the source files
            };

            _log.Debug("Running tar process...");
            _log.Verbose("  Command: tar");
            _log.Verbose("  Arguments: {0}", processSettings.Arguments.Render());
            _log.Verbose("  Working Directory: {0}", processSettings.WorkingDirectory.FullPath);

            var exitCode = _ctx.StartProcess("tar", processSettings);

            if (exitCode != 0)
            {
                return new CopierError($"tar command failed with exit code {exitCode} while creating archive {archivePath.FullPath}.");
            }

            _log.Information("Successfully created Unix archive: {0} ({1} artifacts)", archivePath.GetFilename(), runtimeOrPrimaryArtifacts.Count);
            return CopierResult.ToSuccess();
        }
        catch (Exception ex)
        {
            return new CopierError($"Failed to create tar archive {archivePath.FullPath}: {ex.Message}", ex);
        }
        finally
        {
            // Clean up temporary file list
            if (_ctx.FileExists(fileListPath))
            {
                _log.Debug("Deleting temporary file list: {0}", fileListPath.FullPath);
                _ctx.DeleteFile(fileListPath);
            }
        }
    }

    private async Task<FilePath> CreateFileListAsync(IReadOnlyList<NativeArtifact> artifactsToArchive, DirectoryPath baseDirForTar, CancellationToken ct)
    {
        // Using a subdirectory in the build project's temp folder for more organization
        var tempDir = _ctx.Directory(_ctx.Environment.WorkingDirectory.Combine(".cake/temp/filelists").FullPath);
        _ctx.EnsureDirectoryExists(tempDir);

        var fileListPath = tempDir.Path.CombineWithFilePath($"archive-files-{Guid.NewGuid():N}.txt");

        var relativePaths = artifactsToArchive
            .Select(a => a.SourcePath.GetFilename().FullPath)
            .ToList(); //ToList to count before writing if needed for logging, and for WriteAllLinesAsync

        await File.WriteAllLinesAsync(fileListPath.FullPath, relativePaths, ct);
        _log.Verbose("Created file list for tar: {0} ({1} files), paths relative to {2}",
            fileListPath.GetFilename(),
            relativePaths.Count,
            baseDirForTar.FullPath);

        return fileListPath;
    }
}
