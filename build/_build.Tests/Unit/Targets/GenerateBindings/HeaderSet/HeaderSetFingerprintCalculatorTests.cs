using Build.Targets.GenerateBindings.HeaderSet;
using Build.Tests.Fixtures;

namespace Build.Tests.Unit.Targets.GenerateBindings.HeaderSet;

public sealed class HeaderSetFingerprintCalculatorTests
{
    [Test]
    public async Task ComputeAsync_Should_Normalize_Line_Endings_To_LineFeed_When_Header_Uses_CRLF()
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

    [Test]
    public async Task ComputeAsync_Should_Change_Hash_When_Supplemental_Header_Content_Changes()
    {
        var world = FakeCakeWorld.CreateLinux()
            .WithTextFile("include/SDL2/A.h", "alpha")
            .WithTextFile("supplemental/Extra.h", "extra");
        var include = world.RepoRoot.Combine("include");
        var headers = new ResolvedHeaderSet(include, world.RepoRoot.Combine("supplemental"), include.Combine("SDL2"),
            [include.CombineWithFilePath("SDL2/A.h")]);
        var calculator = new HeaderSetFingerprintCalculator(world.CakeContext);
        var before = await calculator.ComputeAsync(headers);
        world.WithTextFile("supplemental/Extra.h", "changed");
        var after = await calculator.ComputeAsync(headers);
        await Assert.That(before.Hash).IsEqualTo("sha256:d1284ada4ae2426401c89f04e059b7bbfae52808d0cb4827b4217308ddbb6b2e");
        await Assert.That(after.Hash).IsEqualTo("sha256:25ad8f6dfcd3ed2a25562949dd5d588909be980c3ef7cc0cbfa2d0cb29e04db8");
        await Assert.That(before.HeaderCount).IsEqualTo(1);
        await Assert.That(after.HeaderCount).IsEqualTo(1);
    }

    [Test]
    public async Task ComputeAsync_Should_Change_Hash_When_Selected_Header_Content_Changes()
    {
        var world = FakeCakeWorld.CreateLinux()
            .WithTextFile("include/SDL2/A.h", "alpha")
            .WithTextFile("supplemental/Extra.h", "extra");
        var include = world.RepoRoot.Combine("include");
        var headers = new ResolvedHeaderSet(include, world.RepoRoot.Combine("supplemental"), include.Combine("SDL2"),
            [include.CombineWithFilePath("SDL2/A.h")]);
        var calculator = new HeaderSetFingerprintCalculator(world.CakeContext);
        var before = await calculator.ComputeAsync(headers);
        world.WithTextFile("include/SDL2/A.h", "changed");
        var after = await calculator.ComputeAsync(headers);
        await Assert.That(before.Hash).IsEqualTo("sha256:d1284ada4ae2426401c89f04e059b7bbfae52808d0cb4827b4217308ddbb6b2e");
        await Assert.That(after.Hash).IsEqualTo("sha256:dd3f23fd378736128714f3cd8d2c898367698b8b4a3b437206f2e95e4449fc98");
        await Assert.That(before.HeaderCount).IsEqualTo(1);
        await Assert.That(after.HeaderCount).IsEqualTo(1);
    }

    [Test]
    public async Task ComputeAsync_Should_Preserve_Hash_When_Input_Enumeration_Is_Reversed()
    {
        var world = FakeCakeWorld.CreateLinux().WithTextFile("include/SDL2/Z.h", "zeta").WithTextFile("include/SDL2/A.h", "alpha");
        var include = world.RepoRoot.Combine("include");
        var a = include.CombineWithFilePath("SDL2/A.h");
        var z = include.CombineWithFilePath("SDL2/Z.h");
        var headers = new ResolvedHeaderSet(include, world.RepoRoot.Combine("supplemental"), include.Combine("SDL2"), [z, a]);
        var calculator = new HeaderSetFingerprintCalculator(world.CakeContext);
        var before = await calculator.ComputeAsync(headers);
        var after = await calculator.ComputeAsync(headers with { Headers = [a, z] });
        await Assert.That(before.Hash).IsEqualTo("sha256:1aecc0255dd3b82c5db8ae1e0292419db630e8d2762aaaccd867432e2d36e2e8");
        await Assert.That(after.Hash).IsEqualTo("sha256:1aecc0255dd3b82c5db8ae1e0292419db630e8d2762aaaccd867432e2d36e2e8");
        await Assert.That(before.HeaderCount).IsEqualTo(2);
        await Assert.That(after.HeaderCount).IsEqualTo(2);
    }
}
