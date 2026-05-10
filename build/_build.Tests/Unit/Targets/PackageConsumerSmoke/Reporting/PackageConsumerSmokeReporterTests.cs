using Build.Targets.PackageConsumerSmoke.Reporting;
using Build.Tests.Fixtures;

namespace Build.Tests.Unit.Targets.PackageConsumerSmoke.Reporting;

/// <summary>
/// Coverage for <see cref="PackageConsumerSmokeReporter"/> — verifies the IAnsiConsole +
/// ICakeLog cohort behaviour (start banner, per-TFM start/finish/skip, completion rule).
/// Mirrors the HarvestReporter / PackageReporter / ConsolidateHarvestReporter test shape.
/// </summary>
public sealed class PackageConsumerSmokeReporterTests
{
    [Test]
    public async Task LogStarting_Should_Write_Stage_Banner_With_Rid_And_Family_Names()
    {
        var world = FakeCakeWorldV2.CreateWindows();
        var reporter = new PackageConsumerSmokeReporter(world.AnsiConsole, world.Log);

        reporter.LogStarting("win-x64", ["sdl2-core", "sdl2-image"]);

        await Assert.That(world.AnsiConsole.Output).Contains("PackageConsumerSmoke", StringComparison.Ordinal);
        await Assert.That(world.AnsiConsole.Output).Contains("win-x64", StringComparison.Ordinal);
        await Assert.That(world.Log.HasMessage(Cake.Core.Diagnostics.LogLevel.Information, "sdl2-core")).IsTrue();
        await Assert.That(world.Log.HasMessage(Cake.Core.Diagnostics.LogLevel.Information, "sdl2-image")).IsTrue();
    }

    [Test]
    public async Task StartTfm_Should_Emit_Info_Log_With_Tfm()
    {
        var world = FakeCakeWorldV2.CreateWindows();
        var reporter = new PackageConsumerSmokeReporter(world.AnsiConsole, world.Log);

        reporter.StartTfm("net10.0");

        await Assert.That(world.Log.HasMessage(Cake.Core.Diagnostics.LogLevel.Information, "net10.0")).IsTrue();
    }

    [Test]
    public async Task FinishTfm_Should_Emit_Info_Log_With_Tfm()
    {
        var world = FakeCakeWorldV2.CreateWindows();
        var reporter = new PackageConsumerSmokeReporter(world.AnsiConsole, world.Log);

        reporter.FinishTfm("net10.0");

        await Assert.That(world.Log.HasMessage(Cake.Core.Diagnostics.LogLevel.Information, "completed successfully")).IsTrue();
    }

    [Test]
    public async Task ReportSkippedTfm_Should_Emit_Warning_Log_With_Reason()
    {
        var world = FakeCakeWorldV2.CreateWindows();
        var reporter = new PackageConsumerSmokeReporter(world.AnsiConsole, world.Log);

        reporter.ReportSkippedTfm("net462", "Mono not found in PATH");

        await Assert.That(world.Log.HasMessage(Cake.Core.Diagnostics.LogLevel.Warning, "net462")).IsTrue();
        await Assert.That(world.Log.HasMessage(Cake.Core.Diagnostics.LogLevel.Warning, "Mono not found")).IsTrue();
    }

    [Test]
    public async Task LogCompleted_Should_Write_Completion_Rule_With_Counts()
    {
        var world = FakeCakeWorldV2.CreateWindows();
        var reporter = new PackageConsumerSmokeReporter(world.AnsiConsole, world.Log);

        reporter.LogCompleted(tfmsRun: 3, tfmsSkipped: 1);

        await Assert.That(world.AnsiConsole.Output).Contains("complete", StringComparison.Ordinal);
        await Assert.That(world.AnsiConsole.Output).Contains("3 TFMs", StringComparison.Ordinal);
    }

    [Test]
    public void Constructor_Should_Throw_When_Console_Null()
    {
        var world = FakeCakeWorldV2.CreateWindows();
        Assert.Throws<ArgumentNullException>(() => new PackageConsumerSmokeReporter(console: null!, world.Log));
    }

    [Test]
    public void Constructor_Should_Throw_When_Log_Null()
    {
        var world = FakeCakeWorldV2.CreateWindows();
        Assert.Throws<ArgumentNullException>(() => new PackageConsumerSmokeReporter(world.AnsiConsole, log: null!));
    }
}
