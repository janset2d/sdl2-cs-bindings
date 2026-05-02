using Build.Features.Harvesting;

namespace Build.Tests.Unit.Features.Harvesting;

/// <summary>
/// Shape sanity for the <see cref="HarvestRequest"/> record.
/// Harvest is version-blind, so the request carries only the per-RID axis and library filter.
/// </summary>
public sealed class HarvestRequestTests
{
    [Test]
    public async Task Constructor_Should_Hold_Rid_And_Library_Scope()
    {
        var libraries = new List<string> { "SDL2", "SDL2_image" };

        var request = new HarvestRequest(Rid: "win-x64", Libraries: libraries);

        await Assert.That(request.Rid).IsEqualTo("win-x64");
        await Assert.That(request.Libraries).IsSameReferenceAs(libraries);
    }

    [Test]
    public async Task Constructor_Should_Accept_Empty_Library_Scope()
    {
        var request = new HarvestRequest(Rid: "linux-x64", Libraries: []);

        await Assert.That(request.Rid).IsEqualTo("linux-x64");
        await Assert.That(request.Libraries).IsEmpty();
    }
}
