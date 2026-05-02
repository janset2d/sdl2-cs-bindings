using Build.Tools.Ldd;
using Cake.Core;
using Cake.Core.IO;
using Cake.Core.Tooling;

namespace Build.Tools.Tar;

/// <summary>
/// Runs the platform <c>tar</c> binary for gzip extraction. Follows the
/// <see cref="LddRunner"/> triad (Settings / Tool / Aliases).
/// </summary>
public sealed class TarExtractTool : Tool<TarExtractSettings>
{
    public TarExtractTool(
        IFileSystem fileSystem,
        ICakeEnvironment environment,
        IProcessRunner processRunner,
        IToolLocator tools) : base(fileSystem, environment, processRunner, tools)
    {
    }

    protected override string GetToolName() => "tar";

    protected override IEnumerable<string> GetToolExecutableNames() => ["tar", "tar.exe"];

    public void Extract(TarExtractSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        var args = BuildArguments(settings);
        Run(settings, args);
    }

    private static ProcessArgumentBuilder BuildArguments(TarExtractSettings settings)
    {
        var builder = new ProcessArgumentBuilder();

        // -x extract, -z gzip-decompress, -f <archive> input file.
        // -p (preserve permissions) is implicit when tar is run as root on GNU tar; we rely on
        // the default behaviour on BSD tar (macOS / Win10+ bsdtar) to preserve mode bits + symlinks.
        // Both GNU and BSD tar preserve symlinks in -xzf without extra flags.
        var extractFlags = "-xz";
        if (settings.Verbose)
        {
            extractFlags += "v";
        }

        extractFlags += "f";
        builder.Append(extractFlags);
        builder.AppendQuoted(settings.ArchivePath.FullPath);

        if (settings.StripComponents is { } strip)
        {
            builder.Append($"--strip-components={strip.ToString(System.Globalization.CultureInfo.InvariantCulture)}");
        }

        builder.Append("-C");
        builder.AppendQuoted(settings.DestinationDirectory.FullPath);

        return builder;
    }
}
