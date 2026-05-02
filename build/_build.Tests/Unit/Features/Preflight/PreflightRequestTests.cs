using Build.Features.Preflight;
using NuGet.Versioning;

namespace Build.Tests.Unit.Features.Preflight;

/// <summary>
/// Shape sanity for the <see cref="PreflightRequest"/> record.
/// PreFlight always receives the resolved version mapping.
/// </summary>
public sealed class PreflightRequestTests
{
    [Test]
    public async Task Constructor_Should_Hold_Supplied_Versions_Mapping()
    {
        var versions = new Dictionary<string, NuGetVersion>(StringComparer.OrdinalIgnoreCase)
        {
            ["sdl2-core"] = NuGetVersion.Parse("2.32.0-local.20260421T120000"),
            ["sdl2-image"] = NuGetVersion.Parse("2.8.0-local.20260421T120000"),
        };

        var request = new PreflightRequest(versions);

        await Assert.That(request.Versions).IsSameReferenceAs(versions);
        await Assert.That(request.Versions["sdl2-core"].ToNormalizedString()).IsEqualTo("2.32.0-local.20260421T120000");
    }
}
