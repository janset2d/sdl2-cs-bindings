using Build.Domain.Packaging.Models;
using NuGet.Versioning;

namespace Build.Tests.Unit.Domain.Packaging;

/// <summary>
/// Shape sanity for the ADR-003 §3.2 <see cref="PackRequest"/> record. The mapping's key set
/// IS the pack scope (ADR-003 §2.2 "scope = versions.keys").
/// </summary>
public sealed class PackRequestTests
{
    [Test]
    public async Task Constructor_Should_Hold_Versions_Mapping()
    {
        var versions = new Dictionary<string, NuGetVersion>(StringComparer.OrdinalIgnoreCase)
        {
            ["sdl2-core"] = NuGetVersion.Parse("2.32.0-ci.run-id-12345"),
        };

        var request = new PackRequest(versions);

        await Assert.That(request.Versions).IsSameReferenceAs(versions);
        await Assert.That(request.Versions.Count).IsEqualTo(1);
    }
}
