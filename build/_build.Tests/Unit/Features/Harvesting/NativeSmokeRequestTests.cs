using Build.Features.Harvesting;

namespace Build.Tests.Unit.Features.Harvesting;

/// <summary>
/// Shape sanity for the <see cref="NativeSmokeRequest"/> record.
/// The request carries the caller-controlled RID only.
/// </summary>
public sealed class NativeSmokeRequestTests
{
    [Test]
    public async Task Constructor_Should_Hold_Rid()
    {
        var request = new NativeSmokeRequest(Rid: "osx-x64");

        await Assert.That(request.Rid).IsEqualTo("osx-x64");
    }
}
