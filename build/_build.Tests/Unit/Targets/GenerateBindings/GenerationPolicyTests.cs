using System.Text.Json;
using Build.Targets.GenerateBindings;
using Build.Tests.Fixtures;
using Cake.Core.IO;
using TUnit.Assertions.Enums;

namespace Build.Tests.Unit.Targets.GenerateBindings;

public sealed class GenerationPolicyTests
{
    [Test]
    public async Task Headers_Should_Use_Declared_Order_Not_Insertion_Or_Name_Order()
    {
        var family = OracleFamilyFixture.Profile("sdl2-core");
        var entry = family.HeaderSet!.Headers[0];
        family = family with { HeaderSet = family.HeaderSet with { Headers = [entry with { Order = 20, Name = "A.h" }, entry with { Order = 10, Name = "Z.h" }] } };
        await Assert.That(GenerationPlan.Headers(family)).IsEquivalentTo(["Z.h", "A.h"], CollectionOrdering.Matching);
    }

    [Test]
    public async Task Invocations_Should_Keep_The_Recorded_Nine_Pass_Order_For_Every_Backend_And_Family()
    {
        var repository = OracleFamilyFixture.Repository(FakeCakeWorld.CreateLinux().WithManifestObject(ManifestFixture.RealManifest));
        var families = repository.EnumerateEnabledFamilies().Select(id => repository.Load(id).Value).ToArray();
        var expected = JsonSerializer.Deserialize<string[]>(FixtureLoader.Load("BindingGeneration/Generation/postprocess-order.json"))!;
        await Assert.That(BindingPostProcess.Invocations(families).Select(invocation => $"{invocation.Mode} {invocation.Codegen} {invocation.Family.FamilyId}"))
            .IsEquivalentTo(expected, CollectionOrdering.Matching);
    }

    [Test]
    [Arguments(0, false, 0L, false, false, true)]
    [Arguments(0, true, 0L, false, true, false)]
    [Arguments(0, true, 1L, false, false, false)]
    [Arguments(1, false, 0L, true, false, true)]
    [Arguments(1, true, 0L, true, false, true)]
    [Arguments(1, true, 1L, true, false, false)]
    public async Task Classify_Should_Preserve_Output_State_Table(int code, bool exists, long length, bool failure, bool noOp, bool empty)
    {
        var output = ClangSharpOutputClassifier.Classify(code, exists, length, "SDL.h", "");
        await Assert.That(output.Accepted).IsFalse();
        await Assert.That(output.Failed).IsEqualTo(failure);
        await Assert.That(output.NoOp).IsEqualTo(noOp);
        await Assert.That(output.Empty).IsEqualTo(empty);
        await Assert.That(output.Macros).IsEmpty();
        await Assert.That(output.Diagnostics).IsEmpty();
    }

    [Test]
    [Arguments(GenerationScenarioData.MacroWarning, true)]
    [Arguments(GenerationScenarioData.BuiltinWarning, false)]
    [Arguments(GenerationScenarioData.UnknownWarning, false)]
    [Arguments("Warning (Line 1, Column 1): Something else happened.", false)]
    [Arguments("Warning (Line 120, Column 18): Unsupported attribute: 'AnalyzerNoReturn'.", false)]
    [Arguments(GenerationScenarioData.Fatal, false)]
    [Arguments("Skipping 'SDL.h' due to one or more errors listed above.", false)]
    [Arguments("Parsing failed for 'declspec' due to 'CXError_Failure'\nSkipping 'declspec' due to one or more errors listed above.", false)]
    [Arguments("", false)]
    public async Task Classify_Should_Accept_Only_Known_Warning_Streams(string diagnostics, bool accepted)
    {
        var output = ClangSharpOutputClassifier.Classify(6, true, 0, "SDL.h", diagnostics);
        await Assert.That(output.Accepted).IsEqualTo(accepted);
        await Assert.That(output.Failed).IsEqualTo(!accepted);
        await Assert.That(output.Empty).IsEqualTo(!accepted);
        await Assert.That(output.NoOp).IsFalse();
        await Assert.That(output.Macros).IsEquivalentTo(accepted ? ["SDL_MACRO"] : Array.Empty<string>(), CollectionOrdering.Matching);
    }

    [Test]
    public async Task Classify_Should_Preserve_Macro_And_Builtin_Diagnostic_Order_With_Framing()
    {
        var input = string.Join('\n', "  Diagnostics for 'SDL.h':  ", "Processing 'SDL.h'", "Diagnostics for binding generation of SDL.h:",
            GenerationScenarioData.BuiltinWarning, "warning: redefining builtin macro [-Wbuiltin-macro-redefined]",
            GenerationScenarioData.MacroWarning.Replace("SDL_MACRO", "SDL_ZETA", StringComparison.Ordinal),
            GenerationScenarioData.MacroWarning.Replace("(Line", "(C:/path/SDL.h: Line", StringComparison.Ordinal));
        var output = ClangSharpOutputClassifier.Classify(2, true, 1, "SDL.h", input);
        await Assert.That(output.Accepted).IsTrue();
        await Assert.That(output.Macros).IsEquivalentTo(["SDL_ZETA", "SDL_MACRO"], CollectionOrdering.Matching);
        await Assert.That(output.Diagnostics)
            .IsEquivalentTo([GenerationScenarioData.BuiltinWarning, "warning: redefining builtin macro [-Wbuiltin-macro-redefined]"], CollectionOrdering.Matching);
    }

    [Test]
    [Arguments(false, false, false, 0)]
    [Arguments(false, false, true, 4)]
    [Arguments(false, true, false, 3)]
    [Arguments(false, true, true, 3)]
    [Arguments(true, false, false, 2)]
    [Arguments(true, false, true, 2)]
    [Arguments(true, true, false, 2)]
    [Arguments(true, true, true, 2)]
    public async Task ExitCode_Should_Prioritize_ClangSharp_Then_Postprocess_Then_Empty(bool clang, bool postprocess, bool empty, int expected)
        => await Assert.That(ClangSharpOutputClassifier.ExitCode(clang, postprocess, empty)).IsEqualTo(expected);

    [Test]
    public async Task ResponseFiles_Should_Keep_Base_Family_And_Optional_Per_Header_Order()
    {
        var world = FakeCakeWorld.CreateLinux()
            .WithManifestObject(ManifestFixture.RealManifest)
            .WithTextFile("build/binding-generation/rsp/per-header/SDL_audio.rsp", "--namespace\nsame-key-not-parsed\n");
        var repository = OracleFamilyFixture.Repository(world);
        var family = repository.Load("sdl2-core").Value;
        var plan = new GenerationPlan(world.CreateBuildContext(), repository.LoadGlobalSettings(), [family]);
        await Assert.That(plan.ResponseFiles(family, "SDL_audio.h").Select(path => path.FullPath))
            .IsEquivalentTo(["/repo/build/binding-generation/rsp/base.rsp", "/repo/build/binding-generation/rsp/sdl2-core.rsp",
                "/repo/build/binding-generation/rsp/per-header/SDL_audio.rsp"], CollectionOrdering.Matching);
        await Assert.That(plan.ResponseFiles(family, "SDL_video.h").Select(path => path.FullPath))
            .IsEquivalentTo(["/repo/build/binding-generation/rsp/base.rsp", "/repo/build/binding-generation/rsp/sdl2-core.rsp"], CollectionOrdering.Matching);
        await Assert.That(() => plan.ResponseFiles(family, "nested/SDL_audio.h")).Throws<Cake.Core.CakeException>();
        await Assert.That(() => plan.ResponseFiles(family, "nested\\SDL_audio.h")).Throws<Cake.Core.CakeException>();
    }

    [Test]
    public async Task Invocation_Should_Preserve_Define_Undefine_Exclusion_And_Shim_Inputs()
    {
        var world = FakeCakeWorld.CreateLinux().WithManifestObject(ManifestFixture.RealManifest).WithGeneration(platformHeaderShims: true);
        var repository = OracleFamilyFixture.Repository(world);
        var family = repository.Load("sdl2-core").Value;
        var settings = repository.LoadGlobalSettings() with { AllPlatformMacros = ["ZETA", "LINUX", "ALPHA"] };
        var plan = new GenerationPlan(world.CreateBuildContext(), settings, [family]);
        var view = settings.PlatformViews[0] with { Defines = ["LINUX=1"] };
        var platform = plan.Invocation(family, "Modern", "SDL_system.h", new DirectoryPath("/staging/Generated"), view, ["SDL_Z", "SDL_A"]);
        await Assert.That(platform.Config).IsEquivalentTo(["latest-codegen", "windows-types", "generate-macro-bindings"], CollectionOrdering.Matching);
        await Assert.That(platform.Defines).IsEquivalentTo(["LINUX=1"], CollectionOrdering.Matching);
        await Assert.That(platform.Undefines).IsEquivalentTo(["ZETA", "ALPHA"], CollectionOrdering.Matching);
        await Assert.That(platform.Exclusions).IsEquivalentTo(["SDL_Z", "SDL_A"], CollectionOrdering.Matching);
        await Assert.That(platform.IncludeDirectories.Select(path => path.FullPath)).IsEquivalentTo([
            "/repo/vcpkg_installed/x64-linux-hybrid/include/SDL2", "/repo/build/binding-generation/shims/platform-headers"], CollectionOrdering.Matching);
        var neutral = plan.Invocation(family, "Compat", "SDL_system.h", new DirectoryPath("/staging/Generated"));
        await Assert.That(neutral.Undefines).IsEquivalentTo(["ZETA", "LINUX", "ALPHA"], CollectionOrdering.Matching);
        await Assert.That(neutral.Defines).IsEmpty();
        await Assert.That(neutral.Exclusions).IsEmpty();
        await Assert.That(plan.Invocation(family, "Compat", "SDL_audio.h", new DirectoryPath("/staging/Generated")).IncludeDirectories.Select(path => path.FullPath))
            .IsEquivalentTo(["/repo/vcpkg_installed/x64-linux-hybrid/include/SDL2"], CollectionOrdering.Matching);
    }

    [Test]
    public async Task Extract_Should_Keep_First_Declaration_Order_And_Deduplicate_Neutral_Names()
    {
        var source = FixtureLoader.Load("BindingGeneration/Generation/neutral-system.txt");
        var names = NeutralSymbolExclusions.Extract(source + source);
        await Assert.That(names).IsEquivalentTo(["SDL_IsTablet", "SDL_OnApplicationWillTerminate", "SDL_OnApplicationDidReceiveMemoryWarning",
            "SDL_OnApplicationWillResignActive", "SDL_OnApplicationDidEnterBackground", "SDL_OnApplicationWillEnterForeground", "SDL_OnApplicationDidBecomeActive"],
            CollectionOrdering.Matching);
    }
}
