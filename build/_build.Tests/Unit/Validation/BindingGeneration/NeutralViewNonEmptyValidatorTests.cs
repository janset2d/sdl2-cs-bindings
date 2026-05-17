using Build.Tests.Fixtures;
using Build.Validation.BindingGeneration;
using static Build.Tests.Fixtures.BindingGenerationFixture;

namespace Build.Tests.Unit.Validation.BindingGeneration;

public sealed class NeutralViewNonEmptyValidatorTests
{
    [Test]
    public async Task ValidatorId_Should_Be_Stable_Kebab_Case_String()
    {
        await Assert.That(new NeutralViewNonEmptyValidator().ValidatorId)
            .IsEqualTo("neutral-view-non-empty");
    }

    [Test]
    public async Task ValidateAsync_Should_Pass_When_Neutral_Has_Functions()
    {
        var validator = new NeutralViewNonEmptyValidator();
        var model = ModelWithNeutralFunctions("SDL_Init");

        var report = await validator.ValidateAsync(model, Sdl2CoreConfig(), CancellationToken.None);

        await Assert.That(report.IsValid).IsTrue();
        await Assert.That(report.Count).IsEqualTo(0);
    }

    [Test]
    public async Task ValidateAsync_Should_Fail_When_Neutral_Has_Zero_Functions()
    {
        var validator = new NeutralViewNonEmptyValidator();
        var model = ModelWithNeutralFunctions(/* no functions */);

        var report = await validator.ValidateAsync(model, Sdl2CoreConfig(), CancellationToken.None);

        await Assert.That(report.IsValid).IsFalse();
        await Assert.That(report.Errors.Count).IsEqualTo(1);
        await Assert.That(report.Errors[0].Message).Contains("Neutral parse view returned 0 functions");
    }

    [Test]
    public async Task ValidateAsync_Should_Fail_When_Neutral_View_Missing_Entirely()
    {
        var validator = new NeutralViewNonEmptyValidator();
        var model = ModelWithoutNeutralView();

        var report = await validator.ValidateAsync(model, Sdl2CoreConfig(), CancellationToken.None);

        await Assert.That(report.IsValid).IsFalse();
        await Assert.That(report.Errors.Count).IsEqualTo(1);
        await Assert.That(report.Errors[0].Message).Contains("no Neutral parse view");
    }

    [Test]
    public async Task ValidateAsync_Should_Throw_When_Model_Null()
    {
        var validator = new NeutralViewNonEmptyValidator();

        await Assert.That(async () => await validator.ValidateAsync(null!, Sdl2CoreConfig(), CancellationToken.None))
            .Throws<ArgumentNullException>();
    }

    [Test]
    public async Task ValidateAsync_Should_Throw_When_Config_Null()
    {
        var validator = new NeutralViewNonEmptyValidator();

        await Assert.That(async () => await validator.ValidateAsync(ModelWithNeutralFunctions("SDL_Init"), null!, CancellationToken.None))
            .Throws<ArgumentNullException>();
    }
}
