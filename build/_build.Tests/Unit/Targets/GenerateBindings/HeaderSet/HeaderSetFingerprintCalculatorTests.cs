using Build.Targets.GenerateBindings.HeaderSet;
using Build.Tests.Fixtures;

namespace Build.Tests.Unit.Targets.GenerateBindings.HeaderSet;

public sealed class HeaderSetFingerprintCalculatorTests
{
    [Test]
    public async Task ComputeAsync_Should_Normalize_Line_Endings_To_Current_Environment()
    {
        var world = FakeCakeWorld.CreateLinux();
        var includeRoot = world.RepoRoot.Combine("include");
        var sdl2Root = includeRoot.Combine("SDL2");
        var header = sdl2Root.CombineWithFilePath("SDL.h");
        var calculator = new HeaderSetFingerprintCalculator(world.CakeContext);

        world.WithTextFile(header, "line1\r\nline2\r\n");
        var crlf = await calculator.ComputeAsync(new ResolvedHeaderSet(includeRoot, includeRoot.Combine("synthetic"), sdl2Root, [header]));

        world.WithTextFile(header, "line1\nline2\n");
        var lf = await calculator.ComputeAsync(new ResolvedHeaderSet(includeRoot, includeRoot.Combine("synthetic"), sdl2Root, [header]));

        await Assert.That(lf.Hash).IsEqualTo(crlf.Hash);
        await Assert.That(lf.HeaderCount).IsEqualTo(1);
    }
}
