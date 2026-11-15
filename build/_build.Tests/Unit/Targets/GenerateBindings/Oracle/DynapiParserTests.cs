using Build.Data.BindingGeneration;
using Build.Tests.Fixtures;

namespace Build.Tests.Unit.Targets.GenerateBindings.Oracle;

public sealed class DynapiParserTests
{
    [Test]
    public async Task Parse_Should_Read_The_Legacy_Quoted_Export_Field()
    {
        var exports = DynapiManifestRepository.Parse(FixtureLoader.Load("BindingGeneration/Oracle/SDL2.exports"), "fixture/SDL2.exports").PublicSymbols;

        await Assert.That(exports).Contains("SDL_Init");
        await Assert.That(exports.Count).IsEqualTo(1);
    }
}
