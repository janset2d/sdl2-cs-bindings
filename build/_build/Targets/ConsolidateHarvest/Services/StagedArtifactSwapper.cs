using Cake.Common.IO;
using Cake.Core;
using Cake.Core.IO;

namespace Build.Targets.ConsolidateHarvest.Services;

/// <summary>
/// Atomic swap helper for the staged-replace pattern: writers stage everything to .tmp
/// siblings first, then the swapper deletes the old final + moves the .tmp into place.
/// Not truly atomic — IFile/IDirectory.Move don't expose replace overloads — but a crash
/// mid-flight preserves either the old final (delete-old hasn't happened) or the new
/// just-moved final, never wipes both. The next consolidation run re-stages from scratch
/// if it observes a missing final + present .tmp.
/// </summary>
public sealed class StagedArtifactSwapper(ICakeContext cakeContext)
{
    private readonly ICakeContext _cakeContext = cakeContext ?? throw new ArgumentNullException(nameof(cakeContext));

    /// <summary>
    /// Replaces <paramref name="final"/> with <paramref name="temp"/>. No-op if temp is
    /// missing. Deletes the existing final before move so platforms without rename-replace
    /// semantics behave consistently.
    /// </summary>
    public void SwapAtomic(FilePath final, FilePath temp)
    {
        ArgumentNullException.ThrowIfNull(final);
        ArgumentNullException.ThrowIfNull(temp);

        if (!_cakeContext.FileExists(temp))
        {
            return;
        }

        if (_cakeContext.FileExists(final))
        {
            _cakeContext.DeleteFile(final);
        }

        _cakeContext.FileSystem.GetFile(temp).Move(final);
    }

    /// <summary>
    /// Directory variant of <see cref="SwapAtomic(FilePath, FilePath)"/>. Recursive +
    /// force delete on the existing final so non-empty trees swap cleanly.
    /// </summary>
    public void SwapAtomic(DirectoryPath final, DirectoryPath temp)
    {
        ArgumentNullException.ThrowIfNull(final);
        ArgumentNullException.ThrowIfNull(temp);

        if (!_cakeContext.DirectoryExists(temp))
        {
            return;
        }

        if (_cakeContext.DirectoryExists(final))
        {
            _cakeContext.DeleteDirectory(final, new DeleteDirectorySettings { Recursive = true, Force = true });
        }

        _cakeContext.FileSystem.GetDirectory(temp).Move(final);
    }
}
