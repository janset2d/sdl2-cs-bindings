using Build.Application.Harvesting;

namespace Build.Tests.Unit.Domain.Harvesting;

public class PatternMatchingTests
{
    [Test]
    [Arguments("SDL2.dll", "SDL2.dll", true)]
    [Arguments("SDL2_image.dll", "SDL2_image.dll", true)]
    [Arguments("zlib1.dll", "zlib1.dll", true)]
    public async Task MatchesPattern_Should_Match_Exact_File_Names(string fileName, string pattern, bool expected)
    {
        var result = BinaryClosureWalker.MatchesPattern(fileName, pattern);
        await Assert.That(result).IsEqualTo(expected);
    }

    [Test]
    [Arguments("SDL2.dll", "SDL2.so", false)]
    [Arguments("zlib1.dll", "libz.so", false)]
    [Arguments("libSDL2.dylib", "libSDL2.so", false)]
    public async Task MatchesPattern_Should_Reject_Non_Matching_Names(string fileName, string pattern, bool expected)
    {
        var result = BinaryClosureWalker.MatchesPattern(fileName, pattern);
        await Assert.That(result).IsEqualTo(expected);
    }

    [Test]
    [Arguments("libSDL2-2.0.so.0", "libSDL2*", true)]
    [Arguments("libSDL2.so", "libSDL2*", true)]
    [Arguments("libSDL2-2.0.so.0.3200.4", "libSDL2*", true)]
    [Arguments("libSDL2_image-2.0.so.0", "libSDL2_image*", true)]
    public async Task MatchesPattern_Should_Match_Prefix_Wildcard(string fileName, string pattern, bool expected)
    {
        var result = BinaryClosureWalker.MatchesPattern(fileName, pattern);
        await Assert.That(result).IsEqualTo(expected);
    }

    [Test]
    [Arguments("libSDL2.dylib", "libSDL2*.dylib", true)]
    [Arguments("libSDL2-2.0.dylib", "libSDL2*.dylib", true)]
    [Arguments("libSDL2_image.dylib", "libSDL2_image*.dylib", true)]
    [Arguments("libSDL2.so", "libSDL2*.dylib", false)]
    public async Task MatchesPattern_Should_Match_Prefix_And_Suffix_Wildcard(string fileName, string pattern, bool expected)
    {
        var result = BinaryClosureWalker.MatchesPattern(fileName, pattern);
        await Assert.That(result).IsEqualTo(expected);
    }

    [Test]
    [Arguments("SDL2.DLL", "sdl2.dll", true)]
    [Arguments("sdl2.dll", "SDL2.DLL", true)]
    [Arguments("libSDL2.DYLIB", "libSDL2*.dylib", true)]
    public async Task MatchesPattern_Should_Be_Case_Insensitive(string fileName, string pattern, bool expected)
    {
        var result = BinaryClosureWalker.MatchesPattern(fileName, pattern);
        await Assert.That(result).IsEqualTo(expected);
    }
}
