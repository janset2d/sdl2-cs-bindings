using Cake.Core.IO;
using Cake.Core.Tooling;

namespace Build.Infrastructure.Tools.Tar;

/// <summary>
/// Settings for extracting a gzip-compressed tar archive via the platform <c>tar</c> binary.
/// </summary>
/// <remarks>
/// The wrapper exists for <c>Inspect-HarvestedDependencies</c> to preserve the exact semantics
/// MSBuild production extraction relies on (Unix symlinks, file modes, xattrs). A NuGet library
/// alternative (SharpCompress) was evaluated and rejected because its default symbolic-link
/// handler silently discards SONAME symlinks — see docs/phases/phase-2-release-cycle-orchestration-implementation-plan.md
/// §11 Q9 (re-closed 2026-04-21).
/// </remarks>
public sealed class TarExtractSettings : ToolSettings
{
    public TarExtractSettings(FilePath archivePath, DirectoryPath destinationDirectory)
    {
        ArchivePath = archivePath ?? throw new ArgumentNullException(nameof(archivePath));
        DestinationDirectory = destinationDirectory ?? throw new ArgumentNullException(nameof(destinationDirectory));
    }

    /// <summary>
    /// The archive file to extract (e.g., <c>artifacts/harvest_output/SDL2/runtimes/linux-x64/native/native.tar.gz</c>).
    /// </summary>
    public FilePath ArchivePath { get; }

    /// <summary>
    /// Destination directory; passed as the <c>-C</c> argument. The directory must already exist.
    /// </summary>
    public DirectoryPath DestinationDirectory { get; }

    /// <summary>
    /// Emit the <c>-v</c> flag — tar prints each entry extracted. Useful for operator-facing diagnostics.
    /// </summary>
    public bool Verbose { get; set; }

    /// <summary>
    /// When set, inserts <c>--strip-components=N</c>. Use when the archive wraps entries under a
    /// single top-level directory and you want to extract flat. Left at <c>null</c> no strip is applied.
    /// </summary>
    public int? StripComponents { get; set; }
}
