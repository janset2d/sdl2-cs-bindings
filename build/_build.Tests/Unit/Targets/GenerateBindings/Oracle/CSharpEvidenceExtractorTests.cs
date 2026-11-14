using Build.Targets.GenerateBindings.Oracle;
using Build.Tests.Fixtures;
using TUnit.Assertions.Enums;

namespace Build.Tests.Unit.Targets.GenerateBindings.Oracle;

public sealed class CSharpEvidenceExtractorTests
{
    [Test]
    public async Task Extract_Should_Retain_Imports_Helpers_Libraries_And_Nested_Namespaces()
    {
        var evidence = CSharpEvidenceExtractor.Extract("fixture.cs", FixtureLoader.Load("BindingGeneration/Oracle/mixed-generated.cs.txt"), ["NET5_0_OR_GREATER"]);

        await Assert.That(evidence.Functions.Select(function => (function.ManagedName, function.NativeLibrary, function.ImportKind, function.NamespaceName)))
            .IsEquivalentTo([
                ("SDL_Init", "SDL2", "DllImport", "SDL2"),
                ("ManagedHelper", "", "", "SDL2"),
                ("IMG_Init", "SDL2_image", "LibraryImport", "SDL2.Image"),
                ("SDL_Quit", "SDL2", "DllImport", "SDL2.Image"),
                ("IMG_Quit", "SDL2_image", "DllImport", "SDL2.Image")
            ], CollectionOrdering.Matching);
    }

    [Test]
    public async Task Extract_Should_Retain_Fields_Computed_Constants_Span_Properties_Structs_And_Enums()
    {
        var evidence = CSharpEvidenceExtractor.Extract("fixture.cs", FixtureLoader.Load("BindingGeneration/Oracle/mixed-generated.cs.txt"), ["NET5_0_OR_GREATER"]);

        await Assert.That(evidence.Constants.Select(constant => (constant.Name, constant.Kind)))
            .IsEquivalentTo([
                ("SDL_INIT_TIMER", "Field"), ("SDL_INIT_AUDIO", "Field"), ("SDL_INIT_VIDEO", "Field"), ("SDL_INIT_EVERYTHING", "Field"),
                ("LibName", "Field"), ("LibName", "Field"), ("SDL_HINT_RENDER_DRIVER", "Property")
            ], CollectionOrdering.Matching);
        await Assert.That(evidence.Types.Select(type => (type.Name, type.Kind)))
            .IsEquivalentTo([
                ("SDL_bool", "Enum"), ("SDL_RWops", "Struct"), ("SDLNative", "Class"), ("SDL_imageNative", "Class"),
                ("NestedNamespaceNative", "Class"), ("NestedImageNative", "Class")
            ], CollectionOrdering.Matching);
    }

    [Test]
    public async Task Extract_Should_Resolve_Sdl2Cs_Constant_Backed_Imports()
    {
        var evidence = CSharpEvidenceExtractor.Extract("SDL2.cs", FixtureLoader.Load("BindingGeneration/Oracle/sdl2-cs.cs.txt"), []);
        var function = evidence.Functions.Single(function => function.ManagedName == "SDL_Init");

        await Assert.That(function.NativeLibrary).IsEqualTo("SDL2");
        await Assert.That(function.NativeEntryPoint).IsEqualTo("SDL_Init");
        await Assert.That(function.ImportKind).IsEqualTo("DllImport");
    }

    [Test]
    public async Task Extract_Should_Keep_Managed_And_Native_EntryPoint_Names_Distinct()
    {
        var evidence = CSharpEvidenceExtractor.Extract("entrypoint.cs", FixtureLoader.Load("BindingGeneration/Oracle/entrypoint-import.cs.txt"), []);
        var function = evidence.Functions.Single(function => function.ManagedName == "INTERNAL_SDL_Init");

        await Assert.That(function.NativeEntryPoint).IsEqualTo("SDL_Init");
        await Assert.That(function.NativeLibrary).IsEqualTo("SDL2");
        await Assert.That(function.ImportKind).IsEqualTo("DllImport");
    }
}
