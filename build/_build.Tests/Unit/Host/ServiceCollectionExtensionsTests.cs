using Build;
using Build.Data.Manifest;
using Build.Data.Manifest.Models;
using Build.Host;
using Build.Host.Runtime;
using Build.Tests.Fixtures;
using Cake.Core;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;

namespace Build.Tests.Unit.Host;

public sealed class ServiceCollectionExtensionsTests
{
    [Test]
    public async Task AddHostBuildingBlocks_Should_Load_RuntimeProfile_Manifest_Through_Repository()
    {
        var world = FakeCakeWorldV2.CreateWindows()
            .WithTextFile("build/manifest.json", "not valid json");
        var manifest = ManifestFixture.CreateTestManifestConfig();
        var repository = Substitute.For<IManifestRepository>();
        repository.Load().Returns(manifest);
        var services = CreateServices(world);

        services.AddHostBuildingBlocks(CreateParsedArguments(), world.RepoRoot);
        services.AddSingleton(repository);

        using var provider = services.BuildServiceProvider();
        var resolved = provider.GetRequiredService<IRuntimeProfile>();

        await Assert.That(resolved.Rid).IsEqualTo("win-x64");
        repository.Received(1).Load();
    }

    [Test]
    public async Task AddHostBuildingBlocks_Should_Not_Register_Derived_Manifest_Projection_Config()
    {
        var world = FakeCakeWorldV2.CreateWindows();
        var services = CreateServices(world);

        services.AddHostBuildingBlocks(CreateParsedArguments(), world.RepoRoot);

        using var provider = services.BuildServiceProvider();

        await Assert.That(provider.GetService<RuntimeConfig>()).IsNull();
        await Assert.That(provider.GetService<SystemArtefactsConfig>()).IsNull();
        await Assert.That(provider.GetService<ManifestConfig>()).IsNull();
    }

    private static ServiceCollection CreateServices(FakeCakeWorldV2 world)
    {
        var services = new ServiceCollection();
        services.AddSingleton(world.CakeContext);
        services.AddSingleton(world.CakeContext.Log);
        services.AddSingleton(world.CakeContext.Environment);
        services.AddSingleton(world.CakeContext.FileSystem);
        return services;
    }

    private static ParsedArguments CreateParsedArguments(string rid = "win-x64") => new(
        RepoRoot: null,
        Config: "Release",
        VcpkgDir: null,
        VcpkgInstalledDir: null,
        Library: [],
        Rid: rid,
        Dll: [],
        Suffix: null,
        Scope: [],
        ExplicitVersion: [],
        ExplicitVersions: null,
        VersionsFile: null);
}
