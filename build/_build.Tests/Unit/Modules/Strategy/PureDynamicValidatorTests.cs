using Build.Modules.Strategy;
using Build.Modules.Strategy.Models;
using Build.Tests.Fixtures;

namespace Build.Tests.Unit.Modules.Strategy;

public sealed class PureDynamicValidatorTests
{
    [Test]
    public async Task Validate_Should_Pass_When_Closure_Has_Transitive_Dependencies()
    {
        var validator = new PureDynamicValidator(ValidationMode.Strict);

        var manifest = ManifestFixture.CreateTestSatelliteLibrary();
        var closure = new BinaryClosureBuilder()
            .AddPrimaryFile("C:/vcpkg/bin/SDL2_image.dll", "sdl2-image")
            .AddRuntimeDependency("C:/vcpkg/bin/SDL2.dll", "sdl2", "sdl2-image")
            .AddRuntimeDependency("C:/vcpkg/bin/zlib1.dll", "zlib", "sdl2-image")
            .Build();

        var result = validator.Validate(closure, manifest);

        await Assert.That(result.IsSuccess()).IsTrue();
        await Assert.That(result.ValidationSuccess.HasWarnings).IsFalse();
        await Assert.That(result.ValidationSuccess.Mode).IsEqualTo(ValidationMode.Strict);
    }

    [Test]
    public async Task Validate_Should_Preserve_Configured_Mode_In_Success_Result()
    {
        var validator = new PureDynamicValidator(ValidationMode.Warn);

        var manifest = ManifestFixture.CreateTestSatelliteLibrary();
        var closure = new BinaryClosureBuilder()
            .AddPrimaryFile("C:/vcpkg/bin/SDL2_image.dll", "sdl2-image")
            .Build();

        var result = validator.Validate(closure, manifest);

        await Assert.That(result.IsSuccess()).IsTrue();
        await Assert.That(result.ValidationSuccess.Mode).IsEqualTo(ValidationMode.Warn);
    }
}
