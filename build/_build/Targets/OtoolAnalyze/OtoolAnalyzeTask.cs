using Build.Host;
using Build.Targets.OtoolAnalyze.Reporting;
using Build.Targets.OtoolAnalyze.Services;
using Build.Tools.Otool;
using Cake.Common.IO;
using Cake.Core.Diagnostics;
using Cake.Core.IO;
using Cake.Frosting;

namespace Build.Targets.OtoolAnalyze;

[TaskName("Otool-Analyze")]
public sealed class OtoolAnalyzeTask(OtoolReporter reporter) : AsyncFrostingTask<BuildContext>
{
    private static readonly string[] OsxTriplets = ["x64-osx-dynamic", "arm64-osx-dynamic"];

    private readonly OtoolReporter _reporter = reporter ?? throw new ArgumentNullException(nameof(reporter));

    public override async Task RunAsync(BuildContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        _reporter.WriteAnalysisHeader();

        if (context.Dlls.Count > 0)
        {
            _reporter.WriteModeHeader("Specific Libraries");
            await AnalyzeSpecificLibrariesAsync(context);
        }
        else
        {
            _reporter.WriteModeHeader("Vcpkg Libraries");
            await AnalyzeVcpkgLibrariesAsync(context);
        }
    }

    private async Task AnalyzeSpecificLibrariesAsync(BuildContext context)
    {
        foreach (var libraryPath in context.Dlls)
        {
            var file = context.File(libraryPath);
            if (!context.FileExists(file))
            {
                context.Log.Warning("File not found: {0}", file.Path);
                continue;
            }

            await AnalyzeOneAsync(context, file);
        }
    }

    private async Task AnalyzeVcpkgLibrariesAsync(BuildContext context)
    {
        DirectoryPath? vcpkgLibDir = null;

        foreach (var triplet in OsxTriplets)
        {
            var testDir = context.Paths.GetVcpkgInstalledLibDir(triplet);
            if (context.DirectoryExists(testDir))
            {
                vcpkgLibDir = testDir;
                _reporter.WriteVcpkgFoundForTriplet(triplet);
                break;
            }
        }

        if (vcpkgLibDir is null)
        {
            _reporter.WriteVcpkgNotFoundWarning(OsxTriplets);
            return;
        }

        var dylibFiles = context.GetFiles($"{vcpkgLibDir}/*.dylib").ToList();
        if (dylibFiles.Count == 0)
        {
            _reporter.WriteNoDylibsWarning(vcpkgLibDir);
            return;
        }

        context.Log.Information("Found {0} dylib file(s) to analyze", dylibFiles.Count);

        foreach (var dylib in dylibFiles)
        {
            await AnalyzeOneAsync(context, dylib);
        }
    }

    private async Task AnalyzeOneAsync(BuildContext context, FilePath file)
    {
        try
        {
            var settings = new OtoolSettings(file);
            var rawDeps = await Task.Run(() => context.OtoolDependencies(settings));
            var classified = rawDeps
                .Select(kv => LibraryClassifier.Classify(kv.Key, kv.Value))
                .ToList();

            _reporter.WriteAnalysisSection(file, classified);

            var systemCount = classified.Count(d => d.IsSystem);
            var userCount = classified.Count - systemCount;
            context.Log.Information(
                "Analysis complete for {0}: {1} total dependencies ({2} system, {3} user)",
                file.GetFilename(), classified.Count, systemCount, userCount);
        }
#pragma warning disable CA1031
        catch (Exception ex)
#pragma warning restore CA1031
        {
            context.Log.Error("Failed to analyze {0}: {1}", file.GetFilename(), ex.Message);
        }
    }
}
