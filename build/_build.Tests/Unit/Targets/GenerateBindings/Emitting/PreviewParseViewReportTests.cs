using System.Text.Json;
using Build.Targets.GenerateBindings.Emitting;

namespace Build.Tests.Unit.Targets.GenerateBindings.Emitting;

public sealed class PreviewParseViewReportTests
{
    [Test]
    public async Task PreviewParseViewReport_Should_Round_Trip_Through_System_Text_Json()
    {
        var original = new PreviewParseViewReport(
        [
            new PreviewParseViewReportEntry(
                Name: "Neutral",
                SupportedOSPlatform: null,
                FunctionCount: 2,
                Functions:
                [
                    new PreviewParseViewReportFunction("SDL_Init", "SDL_main.h"),
                    new PreviewParseViewReportFunction("SDL_Quit", "SDL_main.h"),
                ]),
            new PreviewParseViewReportEntry(
                Name: "Linux",
                SupportedOSPlatform: "linux",
                FunctionCount: 1,
                Functions:
                [
                    new PreviewParseViewReportFunction("SDL_LinuxSetThreadPriority", "SDL_system.h"),
                ]),
        ]);

        var options = new JsonSerializerOptions { WriteIndented = true };
        var json = JsonSerializer.Serialize(original, options);
        var round = JsonSerializer.Deserialize<PreviewParseViewReport>(json, options);

        await Assert.That(round).IsNotNull();
        await Assert.That(round!.Views.Count).IsEqualTo(2);
        await Assert.That(round.Views[0].Name).IsEqualTo("Neutral");
        await Assert.That(round.Views[0].Functions.Count).IsEqualTo(2);
        await Assert.That(round.Views[1].SupportedOSPlatform).IsEqualTo("linux");
    }
}
