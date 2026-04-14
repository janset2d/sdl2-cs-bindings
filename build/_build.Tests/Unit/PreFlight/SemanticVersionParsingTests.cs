using Build.Tasks.Preflight;

namespace Build.Tests.Unit.PreFlight;

public class SemanticVersionParsingTests
{
    [Test]
    [Arguments("2.32.10", 2, 32, 10)]
    [Arguments("1.0.4", 1, 0, 4)]
    [Arguments("2.8.8", 2, 8, 8)]
    [Arguments("0.1.0", 0, 1, 0)]
    [Arguments("10.20.30", 10, 20, 30)]
    public async Task ParseSemanticVersion_Should_Extract_Major_Minor_Patch(
        string version, int expectedMajor, int expectedMinor, int expectedPatch)
    {
        var (major, minor, patch) = PreFlightCheckTask.ParseSemanticVersion(version);
        await Assert.That(major).IsEqualTo(expectedMajor);
        await Assert.That(minor).IsEqualTo(expectedMinor);
        await Assert.That(patch).IsEqualTo(expectedPatch);
    }

    [Test]
    [Arguments("2.32.10-alpha")]
    [Arguments("1.0.0-rc1")]
    [Arguments("2.8.8-beta.2")]
    public async Task ParseSemanticVersion_Should_Ignore_PreRelease_Suffix(string version)
    {
        var (major, minor, patch) = PreFlightCheckTask.ParseSemanticVersion(version);
        var cleanVersion = version.Split(['-'], 2)[0];
        var parts = cleanVersion.Split('.');

        await Assert.That(major).IsEqualTo(int.Parse(parts[0], System.Globalization.CultureInfo.InvariantCulture));
        await Assert.That(minor).IsEqualTo(int.Parse(parts[1], System.Globalization.CultureInfo.InvariantCulture));
        await Assert.That(patch).IsEqualTo(int.Parse(parts[2], System.Globalization.CultureInfo.InvariantCulture));
    }

    [Test]
    [Arguments("2.32.10+build.123")]
    [Arguments("1.0.0+20260414")]
    public async Task ParseSemanticVersion_Should_Ignore_Build_Metadata(string version)
    {
        var (major, minor, patch) = PreFlightCheckTask.ParseSemanticVersion(version);
        var cleanVersion = version.Split(['+'], 2)[0];
        var parts = cleanVersion.Split('.');

        await Assert.That(major).IsEqualTo(int.Parse(parts[0], System.Globalization.CultureInfo.InvariantCulture));
        await Assert.That(minor).IsEqualTo(int.Parse(parts[1], System.Globalization.CultureInfo.InvariantCulture));
        await Assert.That(patch).IsEqualTo(int.Parse(parts[2], System.Globalization.CultureInfo.InvariantCulture));
    }

    [Test]
    [Arguments("1.0.0-rc1+build.2024")]
    public async Task ParseSemanticVersion_Should_Handle_PreRelease_Plus_Build(string version)
    {
        var (major, minor, patch) = PreFlightCheckTask.ParseSemanticVersion(version);
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
        Assert.Throws<ArgumentException>(() => PreFlightCheckTask.ParseSemanticVersion(version));
    }
}
