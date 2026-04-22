using Cake.Core;
using Cake.Core.Annotations;
using Cake.Core.IO;

namespace Build.Infrastructure.Tools.Tar;

/// <summary>
/// Cake aliases for the repo-local <c>tar</c> wrapper.
/// </summary>
[CakeAliasCategory("Tar")]
public static class TarAliases
{
    /// <summary>
    /// Extracts a gzip-compressed tar archive into the destination directory using the platform <c>tar</c> binary.
    /// Preserves Unix symlinks / permissions / xattrs natively — matches MSBuild production extraction semantics.
    /// </summary>
    [CakeMethodAlias]
    public static void TarExtract(this ICakeContext context, FilePath archivePath, DirectoryPath destinationDirectory)
    {
        TarExtract(context, new TarExtractSettings(archivePath, destinationDirectory));
    }

    /// <summary>
    /// Extracts a gzip-compressed tar archive using the supplied settings.
    /// </summary>
    [CakeMethodAlias]
    public static void TarExtract(this ICakeContext context, TarExtractSettings settings)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(settings);

        var tool = new TarExtractTool(
            context.FileSystem,
            context.Environment,
            context.ProcessRunner,
            context.Tools);

        tool.Extract(settings);
    }
}
