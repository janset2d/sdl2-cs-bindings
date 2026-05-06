using Build.Host;
using Build.Tests.Fixtures;

namespace Build.Tests.Unit.Host;

public sealed class BuildContextResolveVersionsTests
{
    private static readonly string[] ExpectedScope = ["sdl2-core", "sdl2-image"];
    private static readonly string[] ExpectedExplicitVersionEntries = ["sdl2-core=2.32.0", "sdl2-image=2.8.0"];

    [Test]
    public async Task ResolveVersionsSuffix_Should_Return_Parsed_Suffix()
    {
        var world = FakeCakeWorldV2.CreateWindows()
            .WithSuffix("ci.12345");
        var context = world.CreateBuildContext();

        await Assert.That(context.ResolveVersionsSuffix).IsEqualTo("ci.12345");
    }

    [Test]
    public async Task ResolveVersionsScope_Should_Return_Parsed_Scope()
    {
        var world = FakeCakeWorldV2.CreateWindows()
            .WithScope("sdl2-core", "sdl2-image");
        var context = world.CreateBuildContext();

        await Assert.That(context.ResolveVersionsScope).IsEquivalentTo(ExpectedScope);
    }

    [Test]
    public async Task ExplicitVersionEntries_Should_Return_Parsed_Repeated_Entries()
    {
        var world = FakeCakeWorldV2.CreateWindows()
            .WithExplicitVersion("sdl2-core=2.32.0", "sdl2-image=2.8.0");
        var context = world.CreateBuildContext();

        await Assert.That(context.ExplicitVersionEntries).IsEquivalentTo(ExpectedExplicitVersionEntries);
    }

    [Test]
    public async Task ExplicitVersions_Should_Return_Parsed_Comma_Separated_Entries()
    {
        var world = FakeCakeWorldV2.CreateWindows()
            .WithExplicitVersions("sdl2-core=2.32.0,sdl2-image=2.8.0");
        var context = world.CreateBuildContext();

        await Assert.That(context.ExplicitVersions).IsEqualTo("sdl2-core=2.32.0,sdl2-image=2.8.0");
    }

    [Test]
    public async Task ParsedArguments_Should_Not_Be_Public_Task_Facing_Property()
    {
        var property = typeof(BuildContext).GetProperty("ParsedArguments");

        await Assert.That(property).IsNull();
    }
}
