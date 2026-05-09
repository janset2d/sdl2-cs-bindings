using Build.Results;
using Build.Targets.Harvest.Models;
using Build.Targets.Harvest.Reporting;
using Build.Tests.Fixtures;

namespace Build.Tests.Unit.Targets.Harvest.Reporting;

/// <summary>
/// Coverage for <see cref="HarvestReporter"/> — verifies the visual + log output Harvest
/// emits via the injected <c>IAnsiConsole</c> and <c>ICakeLog</c> seams (start/finish/cancel
/// rules, summary panel, phase-failure markup, leak-report logging).
/// </summary>
public sealed class HarvestReporterTests
{
    private const string LibraryName = "sdl2-core";

    [Test]
    public async Task StartLibrary_Should_Write_Library_Name_To_Console()
    {
        var world = FakeCakeWorldV2.CreateWindows();
        var reporter = new HarvestReporter(world.AnsiConsole, world.Log);

        reporter.StartLibrary(LibraryName);

        await Assert.That(world.AnsiConsole.Output).Contains("Harvest: sdl2-core", StringComparison.Ordinal);
    }

    [Test]
    public async Task FinishLibrary_Should_Render_Summary_Panel_And_Finish_Rule()
    {
        var world = FakeCakeWorldV2.CreateWindows();
        var reporter = new HarvestReporter(world.AnsiConsole, world.Log);
        var stats = CreateStats(primary: 2, runtime: 5, license: 3, deployed: 4, filtered: 1);

        reporter.FinishLibrary(LibraryName, stats);

        await Assert.That(world.AnsiConsole.Output).Contains("sdl2-core", StringComparison.Ordinal);
        await Assert.That(world.AnsiConsole.Output).Contains("Finished Harvest", StringComparison.Ordinal);
        await Assert.That(world.AnsiConsole.Output).Contains("Primary Files", StringComparison.Ordinal);
        await Assert.That(world.AnsiConsole.Output).Contains("Deployed Packages", StringComparison.Ordinal);
    }

    [Test]
    public async Task CancelLibrary_Should_Write_Cancel_Rule_And_Warning_Log()
    {
        var world = FakeCakeWorldV2.CreateWindows();
        var reporter = new HarvestReporter(world.AnsiConsole, world.Log);

        reporter.CancelLibrary(LibraryName);

        await Assert.That(world.AnsiConsole.Output).Contains("Canceled Harvest", StringComparison.Ordinal);
        await Assert.That(world.AnsiConsole.Output).Contains("sdl2-core", StringComparison.Ordinal);
        await Assert.That(world.Log.HasMessage(Cake.Core.Diagnostics.LogLevel.Warning, "canceled")).IsTrue();
    }

    [Test]
    public async Task LogCompleted_Should_Write_Green_Completion_Rule()
    {
        var world = FakeCakeWorldV2.CreateWindows();
        var reporter = new HarvestReporter(world.AnsiConsole, world.Log);

        reporter.LogCompleted();

        await Assert.That(world.AnsiConsole.Output).Contains("Harvest completed successfully", StringComparison.Ordinal);
    }

    [Test]
    public async Task LogStarting_Should_Emit_Info_Log_With_Library_Names()
    {
        var world = FakeCakeWorldV2.CreateWindows();
        var reporter = new HarvestReporter(world.AnsiConsole, world.Log);

        reporter.LogStarting(["sdl2-core", "sdl2-image"]);

        await Assert.That(world.Log.HasMessage(Cake.Core.Diagnostics.LogLevel.Information, "sdl2-core")).IsTrue();
        await Assert.That(world.Log.HasMessage(Cake.Core.Diagnostics.LogLevel.Information, "sdl2-image")).IsTrue();
    }

    [Test]
    public async Task ReportPhaseFailure_Should_Write_Red_Rule_And_Error_Log()
    {
        var world = FakeCakeWorldV2.CreateWindows();
        var reporter = new HarvestReporter(world.AnsiConsole, world.Log);

        reporter.ReportPhaseFailure(LibraryName, "Binary closure", "vcpkg cache miss", exception: null);

        await Assert.That(world.AnsiConsole.Output).Contains("Failed Harvest", StringComparison.Ordinal);
        await Assert.That(world.AnsiConsole.Output).Contains("sdl2-core", StringComparison.Ordinal);
        await Assert.That(world.Log.HasMessage(Cake.Core.Diagnostics.LogLevel.Error, "Binary closure")).IsTrue();
        await Assert.That(world.Log.HasMessage(Cake.Core.Diagnostics.LogLevel.Error, "vcpkg cache miss")).IsTrue();
    }

    [Test]
    public async Task ReportPhaseFailure_Should_Log_Verbose_Exception_Details_When_Exception_Present()
    {
        var world = FakeCakeWorldV2.CreateWindows();
        world.Log.Verbosity = Cake.Core.Diagnostics.Verbosity.Diagnostic;
        var reporter = new HarvestReporter(world.AnsiConsole, world.Log);
        var ex = new InvalidOperationException("boom");

        reporter.ReportPhaseFailure(LibraryName, "Artifact planning", "planner exploded", exception: ex);

        await Assert.That(world.Log.HasMessage(Cake.Core.Diagnostics.LogLevel.Error, "Artifact planning")).IsTrue();
        await Assert.That(world.Log.HasMessage(Cake.Core.Diagnostics.LogLevel.Verbose, "boom")).IsTrue();
    }

    [Test]
    public async Task ReportLeakReport_Should_Log_Each_Error_When_Report_Has_Errors()
    {
        var world = FakeCakeWorldV2.CreateWindows();
        var reporter = new HarvestReporter(world.AnsiConsole, world.Log);
        var report = new ValidationReport([
            new ValidationCheck("LeakCheck", ValidationSeverity.Error, "leak: zlib.dll leaks transitive dep"),
            new ValidationCheck("LeakCheck", ValidationSeverity.Error, "leak: png.dll leaks transitive dep"),
        ]);

        reporter.ReportLeakReport(LibraryName, report);

        await Assert.That(world.Log.HasMessage(Cake.Core.Diagnostics.LogLevel.Error, "Hybrid-static leak")).IsTrue();
        await Assert.That(world.Log.HasMessage(Cake.Core.Diagnostics.LogLevel.Error, "zlib.dll leaks")).IsTrue();
        await Assert.That(world.Log.HasMessage(Cake.Core.Diagnostics.LogLevel.Error, "png.dll leaks")).IsTrue();
    }

    [Test]
    public async Task ReportLeakReport_Should_Log_Each_Warning_When_Report_Has_Warnings()
    {
        var world = FakeCakeWorldV2.CreateWindows();
        var reporter = new HarvestReporter(world.AnsiConsole, world.Log);
        var report = new ValidationReport([
            new ValidationCheck("LeakCheck", ValidationSeverity.Warning, "non-blocking warn"),
        ]);

        reporter.ReportLeakReport(LibraryName, report);

        await Assert.That(world.Log.HasMessage(Cake.Core.Diagnostics.LogLevel.Warning, "non-blocking warn")).IsTrue();
    }

    [Test]
    public async Task Constructor_Should_Throw_When_Console_Is_Null()
    {
        var world = FakeCakeWorldV2.CreateWindows();

        await Assert.That(() => new HarvestReporter(null!, world.Log))
            .Throws<ArgumentNullException>();
    }

    [Test]
    public async Task Constructor_Should_Throw_When_Log_Is_Null()
    {
        var world = FakeCakeWorldV2.CreateWindows();

        await Assert.That(() => new HarvestReporter(world.AnsiConsole, null!))
            .Throws<ArgumentNullException>();
    }

    private static DeploymentStatistics CreateStats(int primary, int runtime, int license, int deployed, int filtered)
    {
        var primaryList = Enumerable.Range(0, primary)
            .Select(i => new FileDeploymentInfo(new Cake.Core.IO.FilePath($"primary{i}.dll"), "pkg", DeploymentLocation.FileSystem))
            .ToList();
        var runtimeList = Enumerable.Range(0, runtime)
            .Select(i => new FileDeploymentInfo(new Cake.Core.IO.FilePath($"runtime{i}.dll"), "pkg", DeploymentLocation.FileSystem))
            .ToList();
        var licenseList = Enumerable.Range(0, license)
            .Select(i => new FileDeploymentInfo(new Cake.Core.IO.FilePath($"copyright{i}"), "pkg", DeploymentLocation.FileSystem))
            .ToList();
        var deployedSet = new HashSet<string>(Enumerable.Range(0, deployed).Select(i => $"pkg{i}"), StringComparer.OrdinalIgnoreCase);
        var filteredSet = new HashSet<string>(Enumerable.Range(0, filtered).Select(i => $"filtered{i}"), StringComparer.OrdinalIgnoreCase);
        return new DeploymentStatistics(
            "sdl2-core",
            primaryList,
            runtimeList,
            licenseList,
            deployedSet,
            filteredSet,
            DeploymentStrategy.DirectCopy);
    }
}
