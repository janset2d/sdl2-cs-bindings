using Build.Host.Cli;

namespace Build.Tests.Unit.Host.Cli;

public sealed class GenerateBindingsOptionsTests
{
    [Test]
    public async Task Parse_Should_Accept_The_Evidence_Switch()
    {
        var root = CakeHostCommandLine.CreateRootCommand((_, _) => Task.FromResult(0));
        var parsed = root.Parse(["--target", "GenerateBindings", "--evidence", "--scope", "sdl2-image", "--scope", "sdl2-core"]);
        await Assert.That(parsed.Errors).IsEmpty();
    }
}
