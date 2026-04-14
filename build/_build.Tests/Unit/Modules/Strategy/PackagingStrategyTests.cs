using Build.Modules.Strategy;
using Build.Modules.Strategy.Models;

namespace Build.Tests.Unit.Modules.Strategy;

public sealed class PackagingStrategyTests
{
    [Test]
    public async Task HybridStaticStrategy_Should_Report_HybridStatic_Model()
    {
        var strategy = new HybridStaticStrategy("sdl2");

        await Assert.That(strategy.Model).IsEqualTo(PackagingModel.HybridStatic);
    }

    [Test]
    public async Task HybridStaticStrategy_Should_Identify_Core_Library()
    {
        var strategy = new HybridStaticStrategy("sdl2");

        await Assert.That(strategy.IsCoreLibrary("sdl2")).IsTrue();
        await Assert.That(strategy.IsCoreLibrary("sdl2-image")).IsFalse();
        await Assert.That(strategy.IsCoreLibrary("zlib")).IsFalse();
    }

    [Test]
    public async Task HybridStaticStrategy_Should_Be_Case_Insensitive_For_Core_Library()
    {
        var strategy = new HybridStaticStrategy("sdl2");

        await Assert.That(strategy.IsCoreLibrary("SDL2")).IsTrue();
        await Assert.That(strategy.IsCoreLibrary("Sdl2")).IsTrue();
    }

    [Test]
    public async Task PureDynamicStrategy_Should_Report_PureDynamic_Model()
    {
        var strategy = new PureDynamicStrategy("sdl2");

        await Assert.That(strategy.Model).IsEqualTo(PackagingModel.PureDynamic);
    }

    [Test]
    public async Task PureDynamicStrategy_Should_Identify_Core_Library()
    {
        var strategy = new PureDynamicStrategy("sdl2");

        await Assert.That(strategy.IsCoreLibrary("sdl2")).IsTrue();
        await Assert.That(strategy.IsCoreLibrary("sdl2-image")).IsFalse();
    }
}
