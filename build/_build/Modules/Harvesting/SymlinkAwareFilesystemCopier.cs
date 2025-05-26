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
        var archiveName = $"natives-{rid}.tar.gz";
        var stagingDir = _ctx.Directory($"./staging/runtimes/{rid}/native");
        var archivePath = stagingDir.Path.CombineWithFilePath(archiveName);

        _log.Information("Creating Unix archive {0} with {1} artifacts", archiveName, artifacts.Count);

        _ctx.EnsureDirectoryExists(stagingDir);

        // Create file list for tar
        var fileListPath = await CreateFileListAsync(artifacts, ct);

        try
        {
            // Create tar.gz with symlink preservation (without path transformation for now)
            var exitCode = _ctx.StartProcess("tar", new ProcessSettings
            {
                Arguments = $"-czf \"{archivePath.FullPath}\" -T \"{fileListPath.FullPath}\"",
                WorkingDirectory = _ctx.Environment.WorkingDirectory
            });

            if (exitCode != 0)
            {
                return new CopierError($"tar command failed with exit code {exitCode}");
            }

            _log.Information("Successfully created Unix archive: {0} ({1} artifacts)", archivePath.GetFilename(), artifacts.Count);
            return CopierResult.ToSuccess();
        }
        catch (Exception ex)
        {
            return new CopierError($"Failed to create tar archive: {ex.Message}", ex);
        }
        finally
        {
            // Clean up temporary file list
            if (_ctx.FileExists(fileListPath))
            {
                _ctx.DeleteFile(fileListPath);
            }
        }
    }

    private async Task<FilePath> CreateFileListAsync(List<NativeArtifact> artifacts, CancellationToken ct)
    {
        var tempDir = _ctx.Directory("./temp");
        _ctx.EnsureDirectoryExists(tempDir);

        var fileListPath = tempDir.Path.CombineWithFilePath($"native-files-{Guid.NewGuid():N}.txt");
        var sourceFiles = artifacts.Select(a => a.SourcePath.FullPath);

        await File.WriteAllLinesAsync(fileListPath.FullPath, sourceFiles, ct);
        _log.Verbose("Created file list: {0} ({1} files)", fileListPath.GetFilename(), artifacts.Count);

        return fileListPath;
    }
}
