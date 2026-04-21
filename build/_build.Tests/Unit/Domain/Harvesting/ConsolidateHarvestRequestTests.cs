using Build.Domain.Harvesting.Models;

namespace Build.Tests.Unit.Domain.Harvesting;

/// <summary>
/// Shape sanity for the ADR-003 §3.2 <see cref="ConsolidateHarvestRequest"/> record. The
/// record is a parameterless marker (paths derive from <c>IPathService</c>); tests lock the
/// type exists + value equality holds so runner signatures can treat it as a first-class
/// request without surprises.
/// </summary>
public sealed class ConsolidateHarvestRequestTests
{
    [Test]
    public async Task Instances_Should_Be_Equal_By_Value()
    {
        var left = new ConsolidateHarvestRequest();
        var right = new ConsolidateHarvestRequest();

        await Assert.That(left).IsEqualTo(right);
    }
}
