using Build.Targets.GenerateBindings.Oracle;
using Build.Tests.Fixtures;

namespace Build.Tests.Unit.Targets.GenerateBindings.Oracle;

public sealed class DynapiParserTests
{
    [Test]
    public async Task Parse_Should_Read_The_Legacy_Quoted_Export_Field()
    {
        var exports = DynapiParser.Parse(FixtureLoader.Load("BindingGeneration/Oracle/SDL2.exports"));

        await Assert.That(exports).Contains("SDL_Init");
        await Assert.That(exports.Count).IsEqualTo(1);
    }
}
