using Build.Targets.PackageConsumerSmoke.Services;
using Build.Data.ProjectMetadata;
using Build.Data;
using Build.Results;
using Build.Targets.PackageConsumerSmoke;
using Build.Tests.Fixtures;
using Build.Validation;
using Cake.Core.IO;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;

namespace Build.Tests.Scenarios.PackageConsumerSmoke;

/// <summary>
/// In-process behavior coverage for <c>PackageConsumerSmokeTask</c>: exercises real task
/// orchestration against the fake Cake world. Happy path validates dotnet build + test
/// invocation surfaces; failure paths cover boundary preconditions, scope drift, and
/// missing version mappings.
/// </summary>
public sealed class PackageConsumerSmokeTaskScenarioTests
{
    [Test]
    public async Task RunAsync_Should_Invoke_Compile_Sanity_And_Smoke_Test_When_Happy_Path()
    {
        var world = NewWorld();
        SeedSmokeProjects(world);
        // Versions match the multi-family fixture: sdl2-core@2.32.0 + sdl2-image@2.8.0.
        SeedFeedNupkgs(world, ("sdl2-core", "2.32.0"), ("sdl2-image", "2.8.0"));

        var result = await CreateHost(world).RunAsync();

        await Assert.That(result.Exception).IsNull();
        await Assert.That(result.Success).IsTrue();

        var dotnetInvocations = world.ProcessInvocations
            .Where(p => string.Equals(p.Command.GetFilename().FullPath, "dotnet", StringComparison.OrdinalIgnoreCase))
            .ToList();

        // Expect: 1 compile-sanity build + 1 test (single net10.0 TFM) + multiple build-server-shutdowns.
        await Assert.That(dotnetInvocations.Any(p => p.Arguments.Contains("build", StringComparison.Ordinal) && p.Arguments.Contains("Compile.NetStandard", StringComparison.OrdinalIgnoreCase))).IsTrue();
        await Assert.That(dotnetInvocations.Any(p => p.Arguments.Contains("test", StringComparison.Ordinal) && p.Arguments.Contains("net10.0", StringComparison.Ordinal))).IsTrue();
    }

    [Test]
    public async Task RunAsync_Should_Throw_When_FamilyVersions_Empty()
    {
        // No --versions-file passed at all — task entry should fail-loud.
        var world = NewWorld();
        world.WithVersionsFile(null);
        SeedSmokeProjects(world);

        var result = await CreateHost(world).RunAsync();

        await Assert.That(result.Success).IsFalse();
        await Assert.That(result.Exception!.Message).Contains("requires --versions-file", StringComparison.Ordinal);
    }

    [Test]
    public async Task RunAsync_Should_Throw_When_Smoke_Project_Missing()
    {
        var world = NewWorld();
        // Only seed compile-sanity + feed; smoke project absent → preconditions validator surfaces.
        world.WithTextFile("tests/smoke-tests/package-smoke/Compile.NetStandard/Compile.NetStandard.csproj", BuildSmokeCsproj("sdl2-core", "sdl2-image"));
        world.WithTextFile("artifacts/packages/.placeholder", "");

        var result = await CreateHost(world).RunAsync();

        await Assert.That(result.Success).IsFalse();
        await Assert.That(result.Exception!.Message).Contains("[PCSP-01]", StringComparison.Ordinal);
        await Assert.That(result.Exception.Message).Contains("smoke project", StringComparison.Ordinal);
    }

    [Test]
    public async Task RunAsync_Should_Throw_When_Csproj_Scope_Misses_Manifest_Family()
    {
        var world = NewWorld();
        // Csproj only references sdl2-core; manifest declares sdl2-core + sdl2-image.
        world.WithTextFile("tests/smoke-tests/package-smoke/PackageConsumer.Smoke/PackageConsumer.Smoke.csproj", BuildSmokeCsproj("sdl2-core"));
        world.WithTextFile("tests/smoke-tests/package-smoke/Compile.NetStandard/Compile.NetStandard.csproj", BuildSmokeCsproj("sdl2-core"));
        SeedFeedNupkgs(world, ("sdl2-core", "2.32.0"), ("sdl2-image", "2.8.0"));

        var result = await CreateHost(world).RunAsync();

        await Assert.That(result.Success).IsFalse();
        await Assert.That(result.Exception!.Message).Contains("scope drift", StringComparison.Ordinal);
    }

    [Test]
    public async Task RunAsync_Should_Throw_When_Required_Nupkg_Missing_From_Feed()
    {
        var world = NewWorld();
        SeedSmokeProjects(world);
        // Multi-family fixture: sdl2-core@2.32.0 + sdl2-image@2.8.0. Feed seeds only sdl2-core
        // pair; sdl2-image native nupkg is deliberately absent.
        world.WithBinaryFile("artifacts/packages/Janset.SDL2.Core.2.32.0.nupkg", []);
        world.WithBinaryFile("artifacts/packages/Janset.SDL2.Core.Native.2.32.0.nupkg", []);
        world.WithBinaryFile("artifacts/packages/Janset.SDL2.Image.2.8.0.nupkg", []);

        var result = await CreateHost(world).RunAsync();

        await Assert.That(result.Success).IsFalse();
        await Assert.That(result.Exception!.Message).Contains("Janset.SDL2.Image.Native.2.8.0.nupkg", StringComparison.Ordinal);
    }

    // ── helpers ────────────────────────────────────────────────────────────────

    private const string VersionsFilePath = "artifacts/resolve-versions/versions.json";

    private static FakeCakeWorldV2 NewWorld()
    {
        return FakeCakeWorldV2.CreateWindows()
            .WithManifestObject(ManifestFixture.CreateTestManifestConfig())
            .WithVersionsFile(VersionsFilePath)
            .WithTextFile(VersionsFilePath, FixtureLoader.Load("Versions/versions-multi-family.json"));
    }

    private static void SeedSmokeProjects(FakeCakeWorldV2 world)
    {
        var csproj = BuildSmokeCsproj("sdl2-core", "sdl2-image");
        world.WithTextFile("tests/smoke-tests/package-smoke/PackageConsumer.Smoke/PackageConsumer.Smoke.csproj", csproj);
        world.WithTextFile("tests/smoke-tests/package-smoke/Compile.NetStandard/Compile.NetStandard.csproj", csproj);
    }

    private static void SeedFeedNupkgs(FakeCakeWorldV2 world, params (string Family, string Version)[] entries)
    {
        // Family-name-to-package-id mapping: sdl2-core → Janset.SDL2.Core; sdl2-image → Janset.SDL2.Image
        foreach (var (family, version) in entries)
        {
            var infix = ToPackageInfix(family);
            world.WithBinaryFile($"artifacts/packages/Janset.{infix}.{version}.nupkg", []);
            world.WithBinaryFile($"artifacts/packages/Janset.{infix}.Native.{version}.nupkg", []);
        }
    }

    private static string ToPackageInfix(string family) =>
        family switch
        {
            "sdl2-core" => "SDL2.Core",
            "sdl2-image" => "SDL2.Image",
            _ => family,
        };

    private static string BuildSmokeCsproj(params string[] managedPackageIdsByFamily)
    {
        var ids = managedPackageIdsByFamily.Select(f => $"Janset.{ToPackageInfix(f)}");
        var refs = string.Join(Environment.NewLine, ids.Select(id => $"    <PackageReference Include=\"{id}\" VersionOverride=\"1.0.0\" />"));
        return $"""
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFramework>net10.0</TargetFramework>
              </PropertyGroup>
              <ItemGroup>
            {refs}
              </ItemGroup>
            </Project>
            """;
    }

    private static TargetTestHostV2<PackageConsumerSmokeTask> CreateHost(FakeCakeWorldV2 world)
    {
        var metadataReader = Substitute.For<IProjectMetadataReader>();
        metadataReader.Read(Arg.Any<FilePath>())
            .Returns(Result<EvaluatedProjectMetadata, ProjectMetadataError>.Success(
                new EvaluatedProjectMetadata(["net10.0"], "Authors", "LICENSE", "icon.png")));

        var runtimeEnvironment = Substitute.For<IDotNetRuntimeEnvironment>();
        runtimeEnvironment.ResolveAsync(Arg.Any<string>(), Arg.Any<IReadOnlyList<string>>(), Arg.Any<CancellationToken>())
            .Returns(new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase));

        return new TargetTestHostV2<PackageConsumerSmokeTask>(world)
            .WithManifest(ManifestFixture.CreateTestManifestConfig())
            .WithServices(services =>
            {
                services.AddData();
                services.AddValidators();
                services.AddPackageConsumerSmoke();

                services.AddSingleton(metadataReader);
                services.AddSingleton(runtimeEnvironment);
            });
    }
}
