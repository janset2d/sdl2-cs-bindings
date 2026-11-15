using Build.Tests.Fixtures;

namespace Build.Tests.Unit.Targets.GenerateBindings.Oracle;

public sealed class ManifestRequiredSurfaceReaderTests
{
    [Test]
    public async Task Load_Should_Read_The_Real_Typed_Required_Surface()
    {
        var world = FakeCakeWorld.CreateLinux().WithManifestObject(ManifestFixture.CreateTestManifestConfig() with
        {
            LibraryManifests = [ManifestFixture.CreateTestCoreLibrary() with { BindingGeneration = OracleFamilyFixture.RequiredProfile }]
        });
        var required = OracleFamilyFixture.Repository(world).Load("sdl2-core").Value;

        await Assert.That(required.RequiredFunctions.Select(function => function.Name)).Contains("SDL_Init");
        await Assert.That(required.RequiredFunctions.Count).IsEqualTo(1);
        await Assert.That(required.RequiredConstants.Select(constant => constant.Name)).Contains("SDL_INIT_VIDEO");
        await Assert.That(required.RequiredConstants.Count).IsEqualTo(1);
    }
}
