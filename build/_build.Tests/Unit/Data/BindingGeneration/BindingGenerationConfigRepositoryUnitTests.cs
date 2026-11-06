using Build.Data.BindingGeneration;
using Build.Data.Manifest;

namespace Build.Tests.Unit.Data.BindingGeneration;

/// <summary>
/// Mock-based unit coverage for <see cref="BindingGenerationConfigRepository"/> —
/// constructor argument validation. Load + EnumerateEnabledFamilies behaviour is
/// covered by <see cref="BindingGenerationConfigRepositoryRoundTripTests"/> using
/// <see cref="ManifestRepository"/> against the fake filesystem.
/// </summary>
public sealed class BindingGenerationConfigRepositoryUnitTests
{
    [Test]
    public async Task Constructor_Should_Throw_When_ManifestRepository_Is_Null()
    {
        await Assert.That(() => new BindingGenerationConfigRepository(null!))
            .Throws<ArgumentNullException>();
    }
}
