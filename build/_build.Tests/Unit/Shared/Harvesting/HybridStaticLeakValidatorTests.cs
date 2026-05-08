using Build.Results;
using Build.Shared.Harvesting;
using Build.Shared.Manifest;
using Build.Shared.Runtime;
using Build.Tests.Fixtures;
using NSubstitute;

namespace Build.Tests.Unit.Shared.Harvesting;

public sealed class HybridStaticLeakValidatorTests
{
    [Test]
    public async Task Validate_Should_Pass_When_Library_Is_Core()
    {
        var profile = Substitute.For<IRuntimeProfile>();
        var validator = new HybridStaticLeakValidator(profile, "sdl2", ValidationMode.Strict);

        var manifest = ManifestFixture.CreateTestCoreLibrary();
        var closure = new BinaryClosureBuilder()
            .AddPrimaryFile("C:/vcpkg/bin/SDL2.dll", "sdl2")
            .Build();

        var report = validator.Validate(closure, manifest);

        await Assert.That(report.IsValid).IsTrue();
        await Assert.That(report.Count).IsEqualTo(0);
    }

    [Test]
    public async Task Validate_Should_Pass_When_Satellite_Has_Only_Core_And_System_Deps()
    {
        var profile = Substitute.For<IRuntimeProfile>();
        profile.IsSystemFile(Arg.Any<string>()).Returns(false);

        var validator = new HybridStaticLeakValidator(profile, "sdl2", ValidationMode.Strict);

        var manifest = ManifestFixture.CreateTestSatelliteLibrary();
        var closure = new BinaryClosureBuilder()
            .AddPrimaryFile("C:/vcpkg/bin/SDL2_image.dll", "sdl2-image")
            .AddRuntimeDependency("C:/vcpkg/bin/SDL2.dll", "sdl2", "sdl2-image")
            .Build();

        var report = validator.Validate(closure, manifest);

        await Assert.That(report.IsValid).IsTrue();
    }

    [Test]
    public async Task Validate_Should_Fail_When_Transitive_Dep_Leaks_In_Strict_Mode()
    {
        var profile = Substitute.For<IRuntimeProfile>();
        profile.IsSystemFile(Arg.Any<string>()).Returns(false);

        var validator = new HybridStaticLeakValidator(profile, "sdl2", ValidationMode.Strict);

        var manifest = ManifestFixture.CreateTestSatelliteLibrary();
        var closure = new BinaryClosureBuilder()
            .AddPrimaryFile("C:/vcpkg/bin/SDL2_image.dll", "sdl2-image")
            .AddRuntimeDependency("C:/vcpkg/bin/SDL2.dll", "sdl2", "sdl2-image")
            .AddRuntimeDependency("C:/vcpkg/bin/zlib1.dll", "zlib", "sdl2-image")
            .Build();

        var report = validator.Validate(closure, manifest);

        await Assert.That(report.IsValid).IsFalse();
        await Assert.That(report.Errors.Count).IsEqualTo(1);
        await Assert.That(report.Errors[0].Severity).IsEqualTo(ValidationSeverity.Error);
        await Assert.That(report.Errors[0].Code).IsEqualTo("G19");
        await Assert.That(report.Errors[0].Message).Contains("zlib1.dll");
    }

    [Test]
    public async Task Validate_Should_Pass_With_Warnings_In_Warn_Mode()
    {
        var profile = Substitute.For<IRuntimeProfile>();
        profile.IsSystemFile(Arg.Any<string>()).Returns(false);

        var validator = new HybridStaticLeakValidator(profile, "sdl2", ValidationMode.Warn);

        var manifest = ManifestFixture.CreateTestSatelliteLibrary();
        var closure = new BinaryClosureBuilder()
            .AddPrimaryFile("C:/vcpkg/bin/SDL2_image.dll", "sdl2-image")
            .AddRuntimeDependency("C:/vcpkg/bin/zlib1.dll", "zlib", "sdl2-image")
            .Build();

        var report = validator.Validate(closure, manifest);

        // Warn mode: IsValid=true (non-blocking) but warnings populated
        await Assert.That(report.IsValid).IsTrue();
        await Assert.That(report.HasWarnings).IsTrue();
        await Assert.That(report.Warnings.Count).IsGreaterThan(0);
        await Assert.That(report.Warnings[0].Severity).IsEqualTo(ValidationSeverity.Warning);
    }

    [Test]
    public async Task Validate_Should_Pass_In_Off_Mode_Even_With_Leaks()
    {
        var profile = Substitute.For<IRuntimeProfile>();
        profile.IsSystemFile(Arg.Any<string>()).Returns(false);

        var validator = new HybridStaticLeakValidator(profile, "sdl2", ValidationMode.Off);

        var manifest = ManifestFixture.CreateTestSatelliteLibrary();
        var closure = new BinaryClosureBuilder()
            .AddPrimaryFile("C:/vcpkg/bin/SDL2_image.dll", "sdl2-image")
            .AddRuntimeDependency("C:/vcpkg/bin/zlib1.dll", "zlib", "sdl2-image")
            .Build();

        var report = validator.Validate(closure, manifest);

        await Assert.That(report.IsValid).IsTrue();
        await Assert.That(report.HasWarnings).IsFalse();
        await Assert.That(report.Count).IsEqualTo(0);
    }

    [Test]
    public async Task Validate_Should_Ignore_System_Files_In_Closure()
    {
        var profile = Substitute.For<IRuntimeProfile>();
        profile.IsSystemFile("kernel32.dll").Returns(true);
        profile.IsSystemFile(Arg.Is<string>(s => !s.Contains("kernel32"))).Returns(false);

        var validator = new HybridStaticLeakValidator(profile, "sdl2", ValidationMode.Strict);

        var manifest = ManifestFixture.CreateTestSatelliteLibrary();
        var closure = new BinaryClosureBuilder()
            .AddPrimaryFile("C:/vcpkg/bin/SDL2_image.dll", "sdl2-image")
            .AddRuntimeDependency("C:/vcpkg/bin/kernel32.dll", "windows", "sdl2-image")
            .Build();

        var report = validator.Validate(closure, manifest);

        await Assert.That(report.IsValid).IsTrue();
    }

    [Test]
    public async Task Validate_Should_Report_All_Violations_As_Separate_Checks()
    {
        var profile = Substitute.For<IRuntimeProfile>();
        profile.IsSystemFile(Arg.Any<string>()).Returns(false);

        var validator = new HybridStaticLeakValidator(profile, "sdl2", ValidationMode.Strict);

        var manifest = ManifestFixture.CreateTestSatelliteLibrary();
        var closure = new BinaryClosureBuilder()
            .AddPrimaryFile("C:/vcpkg/bin/SDL2_image.dll", "sdl2-image")
            .AddRuntimeDependency("C:/vcpkg/bin/zlib1.dll", "zlib", "sdl2-image")
            .AddRuntimeDependency("C:/vcpkg/bin/libpng16.dll", "libpng", "sdl2-image")
            .Build();

        var report = validator.Validate(closure, manifest);

        await Assert.That(report.IsValid).IsFalse();
        await Assert.That(report.Errors.Count).IsEqualTo(2);
        await Assert.That(report.Errors.Any(c => c.Message.Contains("zlib1.dll", StringComparison.Ordinal))).IsTrue();
        await Assert.That(report.Errors.Any(c => c.Message.Contains("libpng16.dll", StringComparison.Ordinal))).IsTrue();
    }
}
