using Build.Modules.Contracts;
using Build.Modules.Strategy;
using Build.Modules.Strategy.Models;
using Build.Modules.Strategy.Results;
using Build.Tests.Fixtures;
using Cake.Core.IO;
using NSubstitute;

namespace Build.Tests.Unit.Modules.Strategy;

public sealed class HybridStaticValidatorTests
{
    [Test]
    public async Task Validate_Should_Pass_When_Library_Is_Core()
    {
        var profile = Substitute.For<IRuntimeProfile>();
        var strategy = new HybridStaticStrategy("sdl2");
        var validator = new HybridStaticValidator(profile, strategy, ValidationMode.Strict);

        var manifest = ManifestFixture.CreateTestCoreLibrary();
        var closure = new BinaryClosureBuilder()
            .AddPrimaryFile("C:/vcpkg/bin/SDL2.dll", "sdl2")
            .Build();

        var result = validator.Validate(closure, manifest);

        await Assert.That(result.IsSuccess()).IsTrue();
        await Assert.That(result.ValidationSuccess.HasWarnings).IsFalse();
    }

    [Test]
    public async Task Validate_Should_Pass_When_Satellite_Has_Only_Core_And_System_Deps()
    {
        var profile = Substitute.For<IRuntimeProfile>();
        profile.IsSystemFile(Arg.Any<FilePath>()).Returns(false);

        var strategy = new HybridStaticStrategy("sdl2");
        var validator = new HybridStaticValidator(profile, strategy, ValidationMode.Strict);

        var manifest = ManifestFixture.CreateTestSatelliteLibrary();
        var closure = new BinaryClosureBuilder()
            .AddPrimaryFile("C:/vcpkg/bin/SDL2_image.dll", "sdl2-image")
            .AddRuntimeDependency("C:/vcpkg/bin/SDL2.dll", "sdl2", "sdl2-image")
            .Build();

        var result = validator.Validate(closure, manifest);

        await Assert.That(result.IsSuccess()).IsTrue();
    }

    [Test]
    public async Task Validate_Should_Fail_When_Transitive_Dep_Leaks_In_Strict_Mode()
    {
        var profile = Substitute.For<IRuntimeProfile>();
        profile.IsSystemFile(Arg.Any<FilePath>()).Returns(false);

        var strategy = new HybridStaticStrategy("sdl2");
        var validator = new HybridStaticValidator(profile, strategy, ValidationMode.Strict);

        var manifest = ManifestFixture.CreateTestSatelliteLibrary();
        var closure = new BinaryClosureBuilder()
            .AddPrimaryFile("C:/vcpkg/bin/SDL2_image.dll", "sdl2-image")
            .AddRuntimeDependency("C:/vcpkg/bin/SDL2.dll", "sdl2", "sdl2-image")
            .AddRuntimeDependency("C:/vcpkg/bin/zlib1.dll", "zlib", "sdl2-image")
            .Build();

        var result = validator.Validate(closure, manifest);

        await Assert.That(result.IsError()).IsTrue();
        await Assert.That(result.ValidationError.Violations.Count).IsEqualTo(1);
        await Assert.That(result.ValidationError.Violations[0].Path.GetFilename().FullPath).IsEqualTo("zlib1.dll");
    }

    [Test]
    public async Task Validate_Should_Pass_With_Warnings_In_Warn_Mode()
    {
        var profile = Substitute.For<IRuntimeProfile>();
        profile.IsSystemFile(Arg.Any<FilePath>()).Returns(false);

        var strategy = new HybridStaticStrategy("sdl2");
        var validator = new HybridStaticValidator(profile, strategy, ValidationMode.Warn);

        var manifest = ManifestFixture.CreateTestSatelliteLibrary();
        var closure = new BinaryClosureBuilder()
            .AddPrimaryFile("C:/vcpkg/bin/SDL2_image.dll", "sdl2-image")
            .AddRuntimeDependency("C:/vcpkg/bin/zlib1.dll", "zlib", "sdl2-image")
            .Build();

        var result = validator.Validate(closure, manifest);

        // Warn mode: Success (non-blocking) but warnings populated
        await Assert.That(result.IsSuccess()).IsTrue();
        await Assert.That(result.ValidationSuccess.HasWarnings).IsTrue();
        await Assert.That(result.ValidationSuccess.Warnings.Count).IsGreaterThan(0);
        await Assert.That(result.ValidationSuccess.Mode).IsEqualTo(ValidationMode.Warn);
    }

    [Test]
    public async Task Validate_Should_Pass_In_Off_Mode_Even_With_Leaks()
    {
        var profile = Substitute.For<IRuntimeProfile>();
        profile.IsSystemFile(Arg.Any<FilePath>()).Returns(false);

        var strategy = new HybridStaticStrategy("sdl2");
        var validator = new HybridStaticValidator(profile, strategy, ValidationMode.Off);

        var manifest = ManifestFixture.CreateTestSatelliteLibrary();
        var closure = new BinaryClosureBuilder()
            .AddPrimaryFile("C:/vcpkg/bin/SDL2_image.dll", "sdl2-image")
            .AddRuntimeDependency("C:/vcpkg/bin/zlib1.dll", "zlib", "sdl2-image")
            .Build();

        var result = validator.Validate(closure, manifest);

        await Assert.That(result.IsSuccess()).IsTrue();
        await Assert.That(result.ValidationSuccess.HasWarnings).IsFalse();
    }

    [Test]
    public async Task Validate_Should_Ignore_System_Files_In_Closure()
    {
        var profile = Substitute.For<IRuntimeProfile>();
        profile.IsSystemFile(new FilePath("C:/vcpkg/bin/kernel32.dll")).Returns(true);
        profile.IsSystemFile(Arg.Is<FilePath>(p => !p.FullPath.Contains("kernel32"))).Returns(false);

        var strategy = new HybridStaticStrategy("sdl2");
        var validator = new HybridStaticValidator(profile, strategy, ValidationMode.Strict);

        var manifest = ManifestFixture.CreateTestSatelliteLibrary();
        var closure = new BinaryClosureBuilder()
            .AddPrimaryFile("C:/vcpkg/bin/SDL2_image.dll", "sdl2-image")
            .AddRuntimeDependency("C:/vcpkg/bin/kernel32.dll", "windows", "sdl2-image")
            .Build();

        var result = validator.Validate(closure, manifest);

        await Assert.That(result.IsSuccess()).IsTrue();
    }

    [Test]
    public async Task Validate_Should_Report_Violations_In_Error_Message()
    {
        var profile = Substitute.For<IRuntimeProfile>();
        profile.IsSystemFile(Arg.Any<FilePath>()).Returns(false);

        var strategy = new HybridStaticStrategy("sdl2");
        var validator = new HybridStaticValidator(profile, strategy, ValidationMode.Strict);

        var manifest = ManifestFixture.CreateTestSatelliteLibrary();
        var closure = new BinaryClosureBuilder()
            .AddPrimaryFile("C:/vcpkg/bin/SDL2_image.dll", "sdl2-image")
            .AddRuntimeDependency("C:/vcpkg/bin/zlib1.dll", "zlib", "sdl2-image")
            .AddRuntimeDependency("C:/vcpkg/bin/libpng16.dll", "libpng", "sdl2-image")
            .Build();

        var result = validator.Validate(closure, manifest);

        await Assert.That(result.IsError()).IsTrue();
        await Assert.That(result.ValidationError.Message).Contains("2 violation(s)");
        await Assert.That(result.ValidationError.Violations.Count).IsEqualTo(2);
    }
}
