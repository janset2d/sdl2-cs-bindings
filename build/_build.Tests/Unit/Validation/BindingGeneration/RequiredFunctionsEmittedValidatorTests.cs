using Build.Tests.Fixtures;
using Build.Validation.BindingGeneration;
using static Build.Tests.Fixtures.BindingGenerationFixture;

namespace Build.Tests.Unit.Validation.BindingGeneration;

public sealed class RequiredFunctionsEmittedValidatorTests
{
    [Test]
    public async Task ValidatorId_Should_Be_Stable_Kebab_Case_String()
    {
        await Assert.That(new RequiredFunctionsEmittedValidator().ValidatorId)
            .IsEqualTo("required-functions-emitted");
    }

    [Test]
    public async Task ValidateAsync_Should_Pass_When_Config_Has_No_Required_Functions()
    {
        // Satellite-shape config: enabled=true (or false) but no required_functions
        // declared → validator returns Empty without consulting the model.
        var validator = new RequiredFunctionsEmittedValidator();
        var config = Sdl2CoreConfig(requiredFunctions: []);

        var report = await validator.ValidateAsync(ModelWithNeutralFunctions(), config, CancellationToken.None);

        await Assert.That(report.IsValid).IsTrue();
        await Assert.That(report.Count).IsEqualTo(0);
    }

    [Test]
    public async Task ValidateAsync_Should_Pass_When_All_Required_Functions_Present_In_Neutral_View()
    {
        var validator = new RequiredFunctionsEmittedValidator();
        var config = Sdl2CoreConfig(requiredFunctions:
        [
            RequiredFunction("SDL_Init"),
            RequiredFunction("SDL_Quit"),
        ]);
        var model = ModelWithNeutralFunctions("SDL_Init", "SDL_Quit", "SDL_GetError");

        var report = await validator.ValidateAsync(model, config, CancellationToken.None);

        await Assert.That(report.IsValid).IsTrue();
    }

    [Test]
    public async Task ValidateAsync_Should_Fail_When_Required_Function_Missing_From_Neutral()
    {
        var validator = new RequiredFunctionsEmittedValidator();
        var config = Sdl2CoreConfig(requiredFunctions:
        [
            RequiredFunction("SDL_Init"),
            RequiredFunction("SDL_Quit"),
            RequiredFunction("SDL_WasInit"),
        ]);
        var model = ModelWithNeutralFunctions("SDL_Init");   // SDL_Quit + SDL_WasInit missing

        var report = await validator.ValidateAsync(model, config, CancellationToken.None);

        await Assert.That(report.IsValid).IsFalse();
        await Assert.That(report.Errors.Count).IsEqualTo(2);
        await Assert.That(report.Errors[0].Message).Contains("SDL_Quit");
        await Assert.That(report.Errors[1].Message).Contains("SDL_WasInit");
    }

    [Test]
    public async Task ValidateAsync_Should_Fail_When_Required_Function_Only_Present_In_Platform_View()
    {
        // Required functions must land in Neutral specifically — a function that
        // ended up only in a platform overlay view doesn't satisfy the contract.
        var validator = new RequiredFunctionsEmittedValidator();
        var config = Sdl2CoreConfig(requiredFunctions: [RequiredFunction("SDL_Init")]);
        var model = ModelWithMultipleViews(
            neutralFunctionNames: [],
            windowsFunctionNames: ["SDL_Init"]);

        var report = await validator.ValidateAsync(model, config, CancellationToken.None);

        await Assert.That(report.IsValid).IsFalse();
        await Assert.That(report.Errors[0].Message).Contains("SDL_Init");
    }

    [Test]
    public async Task ValidateAsync_Should_Throw_When_Model_Null()
    {
        var validator = new RequiredFunctionsEmittedValidator();

        await Assert.That(async () => await validator.ValidateAsync(null!, Sdl2CoreConfig(), CancellationToken.None))
            .Throws<ArgumentNullException>();
    }

    [Test]
    public async Task ValidateAsync_Should_Throw_When_Config_Null()
    {
        var validator = new RequiredFunctionsEmittedValidator();

        await Assert.That(async () => await validator.ValidateAsync(ModelWithNeutralFunctions("SDL_Init"), null!, CancellationToken.None))
            .Throws<ArgumentNullException>();
    }
}
