using Build.Domain.Harvesting.Models;

namespace Build.Tests.Unit.Domain.Harvesting;

/// <summary>
/// Shape sanity for the <see cref="ConsolidateHarvestRequest"/> record.
/// The record is a parameterless marker, and these tests lock in value equality.
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
