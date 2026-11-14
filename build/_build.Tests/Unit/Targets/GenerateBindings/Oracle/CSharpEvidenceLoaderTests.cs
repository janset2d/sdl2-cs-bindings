using Build.Targets.GenerateBindings.Oracle;
using Build.Tests.Fixtures;
using TUnit.Assertions.Enums;

namespace Build.Tests.Unit.Targets.GenerateBindings.Oracle;

public sealed class CSharpEvidenceLoaderTests
{
    [Test]
    public async Task Load_Should_Extract_Present_Text_In_Declaration_Order()
    {
        var source = CSharpEvidenceLoader.Load("fixture.cs", FixtureLoader.Load("BindingGeneration/Oracle/mixed-generated.cs.txt"));

        await Assert.That(source.Status).IsEqualTo(SourceStatus.Present);
        await Assert.That(source.Evidence.Functions.Select(function => function.ManagedName))
            .IsEquivalentTo(["SDL_Init", "ManagedHelper", "IMG_Init", "SDL_Quit", "IMG_Quit"], CollectionOrdering.Matching);
    }

    [Test]
    public async Task LoadPathAsync_Should_Read_Single_Files_Through_Cake()
    {
        var world = FakeCakeWorld.CreateLinux().WithTextFile("src/file.cs", FixtureLoader.Load("BindingGeneration/Oracle/entrypoint-import.cs.txt"));
        var source = await CSharpEvidenceLoader.LoadPathAsync(world.CakeContext, world.RepoRoot, "src/file.cs");

        await Assert.That(source.Status).IsEqualTo(SourceStatus.Present);
        await Assert.That(source.Evidence.Functions.Single().NativeEntryPoint).IsEqualTo("SDL_Init");
        await Assert.That(source.Evidence.Functions.Single().SourcePath).IsEqualTo("/repo/src/file.cs");
    }

    [Test]
    public async Task LoadPathAsync_Should_Read_All_CSharp_Files_Recursively_In_Ordinal_Path_Order()
    {
        var world = FakeCakeWorld.CreateLinux()
            .WithTextFile("src/z.cs", FixtureLoader.Load("BindingGeneration/Oracle/entrypoint-import.cs.txt"))
            .WithTextFile("src/nested/a.g.cs", FixtureLoader.Load("BindingGeneration/Oracle/sdl2-cs.cs.txt"))
            .WithTextFile("src/ignore.txt", FixtureLoader.Load("BindingGeneration/Oracle/mixed-generated.cs.txt"));
        var source = await CSharpEvidenceLoader.LoadPathAsync(world.CakeContext, world.RepoRoot, "src");

        await Assert.That(source.Status).IsEqualTo(SourceStatus.Present);
        await Assert.That(source.Evidence.Functions.Select(function => (function.ManagedName, function.SourcePath)))
            .IsEquivalentTo([("SDL_Init", "/repo/src/nested/a.g.cs"), ("INTERNAL_SDL_Init", "/repo/src/z.cs")], CollectionOrdering.Matching);
    }
}
