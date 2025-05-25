#pragma warning disable CA1031

using Build.Modules.Contracts;
using Build.Modules.Harvesting.Models;
using Build.Modules.Harvesting.Results;
using Cake.Common.IO;
using Cake.Core;
using Cake.Core.Diagnostics;

namespace Build.Modules.Harvesting;

public sealed class SymlinkAwareFilesystemCopier : IFilesystemCopier
{
    private readonly ICakeContext _ctx;
    private readonly IRuntimeProfile _profile;
    private readonly ICakeLog _log;

    public SymlinkAwareFilesystemCopier(ICakeContext ctx, IRuntimeProfile profile)
    {
        _ctx = ctx ?? throw new ArgumentNullException(nameof(ctx));
        _profile = profile ?? throw new ArgumentNullException(nameof(profile));
        _log = ctx.Log;
    }

    public async Task<CopierResult> CopyAsync(IEnumerable<NativeArtifact> artifacts, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(artifacts);

        try
        {
            foreach (var artifact in artifacts)
            {
                ct.ThrowIfCancellationRequested();

                _ctx.EnsureDirectoryExists(artifact.TargetPath.GetDirectory());

                if (_profile.OsFamily == "Windows")
                {
                    // Windows: Simple file copy (no symlinks)
                    _ctx.CopyFile(artifact.SourcePath, artifact.TargetPath);
                    _log.Verbose("copied {0} → {1}", artifact.SourcePath.GetFilename(), artifact.TargetPath);
                }
                else
                {
                    // Unix: Preserve symlinks
                    await CopyWithSymlinksAsync(artifact.SourcePath, artifact.TargetPath, ct);
                }

                await Task.Yield();
            }

            return CopierResult.ToSuccess();
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            return new CopierError($"Error while copying artifacts: {ex.Message}", ex);
        }
    }

    private async Task CopyWithSymlinksAsync(Cake.Core.IO.FilePath source, Cake.Core.IO.FilePath target, CancellationToken ct)
    {
        try
        {
            var sourceInfo = await Task.Run(() => new FileInfo(source.FullPath), ct);
            
            if (sourceInfo.LinkTarget != null)
            {
                // It's a symlink - recreate the symlink
                File.CreateSymbolicLink(target.FullPath, sourceInfo.LinkTarget);
                _log.Verbose("symlinked {0} → {1} (target: {2})", 
                    source.GetFilename(), target.GetFilename(), sourceInfo.LinkTarget);
            }
            else
            {
                // It's a regular file - copy normally
                _ctx.CopyFile(source, target);
                _log.Verbose("copied {0} → {1}", source.GetFilename(), target.GetFilename());
            }
        }
        catch (UnauthorizedAccessException ex)
        {
            _log.Warning("Access denied copying {0}: {1}", source.GetFilename(), ex.Message);
            // Fallback to regular copy
            _ctx.CopyFile(source, target);
        }
        catch (IOException ex)
        {
            _log.Warning("IO error copying {0}: {1}", source.GetFilename(), ex.Message);
            // Fallback to regular copy
            _ctx.CopyFile(source, target);
        }
    }
} 