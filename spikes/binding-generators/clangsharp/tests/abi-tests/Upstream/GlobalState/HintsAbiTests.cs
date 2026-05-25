using Janset.SDL2.AbiTests.Infrastructure.Classification;
using Janset.SDL2.AbiTests.Infrastructure.Sdl.Scopes;

namespace Janset.SDL2.AbiTests.Upstream.GlobalState;

public sealed class HintsAbiTests
{
    [Test]
    [NotInParallel(AbiParallelKeys.Hints)]
    [Category(AbiCategories.UpstreamPort)]
    [Category(AbiCategories.GlobalState)]
    [Category(AbiCategories.SdlHints)]
    [UpstreamSdlTest("test/testautomation_hints.c", "hints_setHint")]
    public async Task SDLSetHint_Should_RoundTrip_Custom_Test_Hint()
    {
        using SdlHintScope hint = new("JANSET_SDL2_ABI_TEST_HINT", "enabled");

        await Assert.That(hint.CurrentValue).IsEqualTo("enabled");
    }
}
