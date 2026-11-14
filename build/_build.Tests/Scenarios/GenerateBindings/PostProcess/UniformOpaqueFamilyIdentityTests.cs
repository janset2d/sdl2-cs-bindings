using Build.Targets.GenerateBindings.PostProcess;
using Build.Tests.Fixtures;

namespace Build.Tests.Scenarios.GenerateBindings.PostProcess;

public sealed class UniformOpaqueFamilyIdentityTests
{
    [Test]
    public async Task Resolve_Should_Prefer_The_Project_Path_Over_The_Namespace_Fallback()
    {
        var world = PostProcessFixture.World();
        var config = await PostProcessFixture.ConfigAsync(world);
        var scratch = world.RepoRoot.Combine("scratch").FullPath;
        await Assert.That(UniformOpaqueFamilyIdentity.Resolve(scratch, "SDL2", config)).IsEqualTo("core");
        await Assert.That(UniformOpaqueFamilyIdentity.Resolve(scratch, "SDL2.Ttf", config)).IsEqualTo("ttf");
        var mixer = world.RepoRoot.Combine("Janset.SDL2.Mixer/Generated/Modern").FullPath;
        await Assert.That(UniformOpaqueFamilyIdentity.Resolve(mixer, "SDL2", config)).IsEqualTo("mixer");
    }
}
