using Build.Targets.GenerateBindings.Oracle;
using Build.Tests.Fixtures;

namespace Build.Tests.Unit.Targets.GenerateBindings.Oracle;

public sealed class ManifestRequiredSurfaceReaderTests
{
    [Test]
    public async Task ReadJson_Should_Read_Header_Keyed_Required_Names()
    {
        var required = ManifestRequiredSurfaceReader.ReadJson(FixtureLoader.Load("BindingGeneration/Oracle/required-surface.json"));

        await Assert.That(required.Status).IsEqualTo(SourceStatus.Present);
        await Assert.That(required.RequiredFunctions).Contains("SDL_Init");
        await Assert.That(required.RequiredFunctions.Count).IsEqualTo(1);
        await Assert.That(required.RequiredConstants).Contains("SDL_INIT_VIDEO");
        await Assert.That(required.RequiredConstants.Count).IsEqualTo(1);
    }
}
