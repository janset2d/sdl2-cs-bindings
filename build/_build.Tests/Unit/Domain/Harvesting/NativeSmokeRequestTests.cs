using Build.Domain.Harvesting.Models;

namespace Build.Tests.Unit.Domain.Harvesting;

/// <summary>
/// Shape sanity for the ADR-003 §3.2 <see cref="NativeSmokeRequest"/> record. NativeSmoke
/// is the per-RID native payload validation stage extracted from Harvest in Slice D — the
/// request surfaces the only caller-controlled axis (RID).
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
