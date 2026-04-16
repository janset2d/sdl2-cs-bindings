using Build.Modules.Preflight;

namespace Build.Tests.Unit.Tasks.Preflight;

public class SemanticVersionParsingTests
{
    [Test]
    [Arguments("2.32.10", 2, 32, 10)]
    [Arguments("1.0.4", 1, 0, 4)]
    [Arguments("2.8.8", 2, 8, 8)]
    [Arguments("0.1.0", 0, 1, 0)]
    [Arguments("10.20.30", 10, 20, 30)]
    public async Task ParseSemanticVersion_Should_Extract_Major_Minor_Patch(string version, int expectedMajor, int expectedMinor, int expectedPatch)
    {
        var (major, minor, patch) = VersionConsistencyValidator.ParseSemanticVersion(version);
        await Assert.That(major).IsEqualTo(expectedMajor);
        await Assert.That(minor).IsEqualTo(expectedMinor);
        await Assert.That(patch).IsEqualTo(expectedPatch);
    }

    [Test]
    [Arguments("2.32.10-alpha", 2, 32, 10)]
    [Arguments("1.0.0-rc1", 1, 0, 0)]
    [Arguments("2.8.8-beta.2", 2, 8, 8)]
    public async Task ParseSemanticVersion_Should_Ignore_PreRelease_Suffix(string version, int expectedMajor, int expectedMinor, int expectedPatch)
    {
        var (major, minor, patch) = VersionConsistencyValidator.ParseSemanticVersion(version);

        await Assert.That(major).IsEqualTo(expectedMajor);
        await Assert.That(minor).IsEqualTo(expectedMinor);
        await Assert.That(patch).IsEqualTo(expectedPatch);
    }

    [Test]
    [Arguments("2.32.10+build.123", 2, 32, 10)]
    [Arguments("1.0.0+20260414", 1, 0, 0)]
    public async Task ParseSemanticVersion_Should_Ignore_Build_Metadata(string version, int expectedMajor, int expectedMinor, int expectedPatch)
    {
        var (major, minor, patch) = VersionConsistencyValidator.ParseSemanticVersion(version);

        await Assert.That(major).IsEqualTo(expectedMajor);
        await Assert.That(minor).IsEqualTo(expectedMinor);
        await Assert.That(patch).IsEqualTo(expectedPatch);
    }

    [Test]
    [Arguments("1.0.0-rc1+build.2024")]
    public async Task ParseSemanticVersion_Should_Handle_PreRelease_Plus_Build(string version)
    {
        var (major, minor, patch) = VersionConsistencyValidator.ParseSemanticVersion(version);
        await Assert.That(major).IsEqualTo(1);
        await Assert.That(minor).IsEqualTo(0);
        await Assert.That(patch).IsEqualTo(0);
    }

    [Test]
    [Arguments("2.x.3")]
    [Arguments("abc")]
    [Arguments("2.1")]
    [Arguments("")]
    public void ParseSemanticVersion_Should_Throw_When_Format_Is_Invalid(string version)
    {
        Assert.Throws<ArgumentException>(() => VersionConsistencyValidator.ParseSemanticVersion(version));
    }
}
