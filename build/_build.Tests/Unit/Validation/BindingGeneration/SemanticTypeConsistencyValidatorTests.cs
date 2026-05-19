using System.Runtime.InteropServices;
using Build.Data.BindingGeneration.Models;
using Build.Targets.GenerateBindings.Model;
using Build.Validation.BindingGeneration;
using static Build.Tests.Fixtures.BindingGenerationFixture;

namespace Build.Tests.Unit.Validation.BindingGeneration;

public sealed class SemanticTypeConsistencyValidatorTests
{
    [Test]
    public async Task ValidatorId_Should_Be_Stable_Kebab_Case_String()
    {
        await Assert.That(new SemanticTypeConsistencyValidator().ValidatorId)
            .IsEqualTo("semantic-type-consistency");
    }

    [Test]
    public async Task ValidateAsync_Should_Fail_When_Unsupported_Type_Appears_In_Model()
    {
        var validator = new SemanticTypeConsistencyValidator();
        var model = new BindingModel(
            Views:
            [
                new BindingParseView(
                    "Neutral",
                    SupportedOsPlatform: null,
                    Functions:
                    [
                        new BindingFunction(
                            "SDL_Broken",
                            NativeVoid(),
                            [new BindingParameter(Unsupported("MysteryHandle"), "value")],
                            "SDL_broken.h"),
                    ]),
            ],
            Structs: [],
            Enums: [],
            Constants: [],
            Handles: [],
            Callbacks: []);

        var report = await validator.ValidateAsync(model, Sdl2CoreConfig(), CancellationToken.None);

        await Assert.That(report.IsValid).IsFalse();
        await Assert.That(report.Errors.Count).IsEqualTo(1);
        await Assert.That(report.Errors[0].Message).Contains("MysteryHandle");
        await Assert.That(report.Errors[0].Message).Contains("SDL_Broken parameter 'value'");
    }

    [Test]
    public async Task ValidateAsync_Should_Fail_When_Deferred_Type_Is_Not_Configured()
    {
        var validator = new SemanticTypeConsistencyValidator();
        var deferred = Deferred("SDL_Unplanned");
        var model = new BindingModel(
            Views: [],
            Structs:
            [
                new BindingStruct(
                    "SDL_UnplannedOwner",
                    [new BindingStructField("payload", NativeTypeRef.Indirection(deferred, 1, "SDL_Unplanned*"), FieldOffset: null)],
                    LayoutKind.Sequential,
                    ExplicitSize: null),
            ],
            Enums: [],
            Constants: [],
            Handles: [],
            Callbacks: []);

        var report = await validator.ValidateAsync(model, Sdl2CoreConfig(), CancellationToken.None);

        await Assert.That(report.IsValid).IsFalse();
        await Assert.That(report.Errors.Count).IsEqualTo(1);
        await Assert.That(report.Errors[0].Message).Contains("SDL_Unplanned");
        await Assert.That(report.Errors[0].Message).Contains("SDL_UnplannedOwner.payload element type");
    }

    [Test]
    public async Task ValidateAsync_Should_Pass_When_Deferred_Type_Is_Configured()
    {
        var validator = new SemanticTypeConsistencyValidator();
        var deferred = Deferred("SDL_SysWMinfo");
        var model = new BindingModel(
            Views:
            [
                new BindingParseView(
                    "Neutral",
                    SupportedOsPlatform: null,
                    Functions:
                    [
                        new BindingFunction(
                            "SDL_GetWindowWMInfo",
                            NativeInt(),
                            [new BindingParameter(NativeTypeRef.Indirection(deferred, 1, "SDL_SysWMinfo*"), "info")],
                            "SDL_syswm.h"),
                    ]),
            ],
            Structs: [],
            Enums: [],
            Constants: [],
            Handles: [],
            Callbacks: []);

        var report = await validator.ValidateAsync(model, Sdl2CoreConfig(), CancellationToken.None);

        await Assert.That(report.IsValid).IsTrue();
        await Assert.That(report.Count).IsEqualTo(0);
    }

    [Test]
    public async Task ValidateAsync_Should_Traverse_Flat_Model_Categories()
    {
        var validator = new SemanticTypeConsistencyValidator();
        var unsupported = Unsupported("UnexpectedEnumStorage");
        var model = new BindingModel(
            Views: [],
            Structs: [new BindingStruct("SDL_BadStruct", [new BindingStructField("field", NativeInt(), FieldOffset: null)], LayoutKind.Sequential, ExplicitSize: null)],
            Enums: [new BindingEnumeration("SDL_BadEnum", unsupported, IsFlags: false, Members: [])],
            Constants: [new BindingConstant("SDL_BAD_CONSTANT", NativeInt(), "1", ConstantKind.Literal)],
            Handles: [new BindingHandle("SDL_Window", NativeTypeRef.OpaqueHandle("SDL_Window", "SDL_Window", "sdl2-core", "SDL_video.h"))],
            Callbacks: [new BindingCallback("SDL_EventFilter", NativeInt(), [new BindingParameter(NativeInt(), "userdata")])]);

        var report = await validator.ValidateAsync(model, Sdl2CoreConfig(), CancellationToken.None);

        await Assert.That(report.IsValid).IsFalse();
        await Assert.That(report.Errors.Count).IsEqualTo(1);
        await Assert.That(report.Errors[0].Message).Contains("SDL_BadEnum underlying type");
    }

    [Test]
    public async Task ValidateAsync_Should_Throw_When_Model_Null()
    {
        var validator = new SemanticTypeConsistencyValidator();

        await Assert.That(async () => await validator.ValidateAsync(null!, Sdl2CoreConfig(), CancellationToken.None))
            .Throws<ArgumentNullException>();
    }

    [Test]
    public async Task ValidateAsync_Should_Throw_When_Config_Null()
    {
        var validator = new SemanticTypeConsistencyValidator();

        await Assert.That(async () => await validator.ValidateAsync(new BindingModel(Views: []), null!, CancellationToken.None))
            .Throws<ArgumentNullException>();
    }

    private static NativeTypeRef Unsupported(string nativeName) =>
        new(
            NativeName: nativeName,
            ManagedName: "IntPtr",
            Kind: NativeTypeKind.Unsupported,
            PointerDepth: 0,
            OwningFamilyId: null,
            SourceHeader: "SDL_broken.h",
            AbiShape: NativeAbiShape.Of("IntPtr", IntPtr.Size),
            ElementType: null,
            Diagnostics:
            [
                new NativeTypeDiagnostic(
                    NativeTypeDiagnosticSeverity.Error,
                    "Fixture unsupported type.",
                    "SDL_broken.h",
                    nativeName),
            ]);

    private static NativeTypeRef Deferred(string nativeName) =>
        new(
            NativeName: nativeName,
            ManagedName: nativeName,
            Kind: NativeTypeKind.Deferred,
            PointerDepth: 0,
            OwningFamilyId: null,
            SourceHeader: "SDL_syswm.h",
            AbiShape: NativeAbiShape.Of("IntPtr", IntPtr.Size),
            ElementType: null,
            Diagnostics: []);
}
