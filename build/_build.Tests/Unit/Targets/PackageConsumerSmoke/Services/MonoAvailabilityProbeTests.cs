using Build.Targets.PackageConsumerSmoke.Services;
using Build.Tests.Fixtures;
using Cake.Core.IO;
using Cake.Testing;

namespace Build.Tests.Unit.Targets.PackageConsumerSmoke.Services;

public sealed class MonoAvailabilityProbeTests
{
    [Test]
    public async Task IsMonoAvailable_Should_Return_True_When_Mono_Found_In_Path()
    {
        var world = FakeCakeWorldV2.CreateLinux();
        world.Environment.SetEnvironmentVariable("PATH", "/usr/local/bin:/usr/bin");
        world.FileSystem.CreateFile(new FilePath("/usr/local/bin/mono"));

        var probe = new MonoAvailabilityProbe(world.CakeContext);

        await Assert.That(probe.IsMonoAvailable()).IsTrue();
    }

    [Test]
    public async Task IsMonoAvailable_Should_Return_False_When_Mono_Not_In_Path()
    {
        var world = FakeCakeWorldV2.CreateLinux();
        world.Environment.SetEnvironmentVariable("PATH", "/usr/bin:/bin");

        var probe = new MonoAvailabilityProbe(world.CakeContext);

        await Assert.That(probe.IsMonoAvailable()).IsFalse();
    }

    [Test]
    public async Task IsMonoAvailable_Should_Return_False_When_Path_Empty()
    {
        var world = FakeCakeWorldV2.CreateLinux();
        world.Environment.SetEnvironmentVariable("PATH", string.Empty);

        var probe = new MonoAvailabilityProbe(world.CakeContext);

        await Assert.That(probe.IsMonoAvailable()).IsFalse();
    }

    [Test]
    public async Task IsMonoAvailable_Should_Skip_Empty_Path_Entries_And_Continue()
    {
        var world = FakeCakeWorldV2.CreateLinux();
        world.Environment.SetEnvironmentVariable("PATH", "::/usr/local/bin:/usr/bin");
        world.FileSystem.CreateFile(new FilePath("/usr/local/bin/mono"));

        var probe = new MonoAvailabilityProbe(world.CakeContext);

        await Assert.That(probe.IsMonoAvailable()).IsTrue();
    }

    [Test]
    public void Constructor_Should_Throw_When_CakeContext_Null()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new MonoAvailabilityProbe(cakeContext: null!));
    }
}
