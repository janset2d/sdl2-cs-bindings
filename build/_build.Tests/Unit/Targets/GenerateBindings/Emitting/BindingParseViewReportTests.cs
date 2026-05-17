using System.Text.Json;
using Build.Targets.GenerateBindings.Emitting;

namespace Build.Tests.Unit.Targets.GenerateBindings.Emitting;

public sealed class BindingParseViewReportTests
{
    [Test]
    public async Task BindingParseViewReport_Should_Round_Trip_Through_System_Text_Json()
    {
        var original = new BindingParseViewReport(
        [
            new BindingParseViewReportEntry(
                Name: "Neutral",
                SupportedOSPlatform: null,
                FunctionCount: 2,
                Functions:
                [
                    new BindingParseViewReportFunction("SDL_Init", "SDL.h"),
                    new BindingParseViewReportFunction("SDL_Quit", "SDL.h"),
                ]),
            new BindingParseViewReportEntry(
                Name: "Linux",
                SupportedOSPlatform: "linux",
                FunctionCount: 1,
                Functions:
                [
                    new BindingParseViewReportFunction("SDL_LinuxSetThreadPriority", "SDL_system.h"),
                ]),
        ]);

        var options = new JsonSerializerOptions { WriteIndented = true };
        var json = JsonSerializer.Serialize(original, options);
        var round = JsonSerializer.Deserialize<BindingParseViewReport>(json, options);

        await Assert.That(round).IsNotNull();
        await Assert.That(round!.Views.Count).IsEqualTo(2);
        await Assert.That(round.Views[0].Name).IsEqualTo("Neutral");
        await Assert.That(round.Views[0].Functions.Count).IsEqualTo(2);
        await Assert.That(round.Views[1].SupportedOSPlatform).IsEqualTo("linux");
    }
}
