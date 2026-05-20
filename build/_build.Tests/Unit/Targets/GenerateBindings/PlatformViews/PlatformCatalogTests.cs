using Build.Targets.GenerateBindings.PlatformViews;

namespace Build.Tests.Unit.Targets.GenerateBindings.PlatformViews;

public sealed class PlatformCatalogTests
{
    [Test]
    public async Task CreateSdl2Catalog_Should_Include_Neutral_And_Required_Core_Platforms()
    {
        var catalog = PlatformCatalog.CreateSdl2Catalog();
        var names = catalog.ParseViews.Select(view => view.Name).ToArray();

        await Assert.That(names).IsEquivalentTo(
        [
            "Neutral",
            "WindowsDesktop",
            "WinRT",
            "GDK",
            "Linux",
            "MacOS",
            "IOS",
            "Android",
        ]);
    }

    [Test]
    public async Task CreateSdl2Catalog_Should_Bundle_Backend_Macros_Into_Os_Views()
    {
        var catalog = PlatformCatalog.CreateSdl2Catalog();
        var linux = catalog.ParseViews.Single(view => view.Name == "Linux");

        await Assert.That(linux.SupportedOsPlatform).IsEqualTo("linux");
        await Assert.That(linux.Defines).Contains("SDL_VIDEO_DRIVER_X11=1");
        await Assert.That(linux.Defines).Contains("SDL_VIDEO_DRIVER_WAYLAND=1");
        await Assert.That(linux.Defines).Contains("SDL_VIDEO_DRIVER_KMSDRM=1");
    }

    [Test]
    public async Task CreateSdl2Catalog_Should_Undefine_Platform_Macros_Not_Defined_By_View()
    {
        var catalog = PlatformCatalog.CreateSdl2Catalog();
        var linux = catalog.ParseViews.Single(view => view.Name == "Linux");

        await Assert.That(linux.Undefines).Contains("_WIN32");
        await Assert.That(linux.Undefines).Contains("__APPLE__");
        await Assert.That(linux.Undefines).DoesNotContain("__linux__");
        await Assert.That(linux.Undefines).DoesNotContain("SDL_VIDEO_DRIVER_X11");
    }
}
