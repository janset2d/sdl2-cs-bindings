using System.Text.Json;
using Build.Context.Configs;
using Build.Context.Models;
using Build.Modules.Contracts;
using Build.Modules.Harvesting.Models;
using Build.Modules.Harvesting.Results;
using Build.Modules.Packaging;
using Build.Modules.Packaging.Models;
using Build.Modules.Packaging.Results;
using Build.Tests.Fixtures;
using Cake.Core.IO;
using Cake.Core.Tooling;
using Cake.Testing;
using NSubstitute;

namespace Build.Tests.Unit.Modules.Packaging;

/// <summary>
/// Post-S1 (2026-04-17): <see cref="PackageTaskRunner"/> runs a 2-step per-family flow —
/// Pack(native) then Pack(managed), both with <c>$(Version)</c>, native additionally with
/// <c>$(NativePayloadSource)</c>. No 4-step orchestration, no <c>BuildProjectReferences=false</c>.
/// </summary>
public sealed class PackageTaskRunnerTests
{
    [Test]
    public async Task RunAsync_Should_Pack_Native_Then_Managed_For_Each_Selected_Family()
    {
        var manifest = ManifestFixture.CreateTestManifestConfig();
        var family = manifest.PackageFamilies.Single(static packageFamily => packageFamily.Name == "sdl2-core");

        var repo = new FakeRepoBuilder(FakeRepoPlatform.Windows)
            .WithManifest(manifest)
            .WithTextFile("artifacts/harvest_output/SDL2/harvest-manifest.json", JsonSerializer.Serialize(CreateHarvestManifest("SDL2")))
            .WithTextFile("artifacts/harvest_output/SDL2/runtimes/win-x64/native/SDL2.dll", "payload")
            .WithTextFile("artifacts/harvest_output/SDL2/licenses/LICENSE.txt", "license")
            .BuildContextWithHandles();

        var processRunner = Substitute.For<IProcessRunner>();
        using var process = new FakeProcess();
        process.SetExitCode(0);
        process.SetStandardOutput(["0123456789abcdef0123456789abcdef01234567"]);
        process.SetStandardError([]);
        processRunner.Start(Arg.Any<FilePath>(), Arg.Any<ProcessSettings>()).Returns(process);
        repo.CakeContext.ProcessRunner.Returns(processRunner);

        var packageFamilySelector = Substitute.For<IPackageFamilySelector>();
        packageFamilySelector.Select(Arg.Any<IReadOnlyList<string>>())
            .Returns(new PackageFamilySelection([family]));

        var packageVersionResolver = Substitute.For<IPackageVersionResolver>();
        packageVersionResolver.Resolve(Arg.Any<string?>()).Returns(new PackageVersion("1.2.3"));

        var dotNetPackInvoker = Substitute.For<IDotNetPackInvoker>();
        dotNetPackInvoker.Pack(
            Arg.Any<FilePath>(),
            Arg.Any<DotNetPackInvocation>(),
            Arg.Any<bool>(),
            Arg.Any<bool>()).Returns(DotNetPackResult.ToSuccess());

        var projectMetadataReader = Substitute.For<IProjectMetadataReader>();
        ProjectMetadataResult metadataResult = new ProjectMetadata(
            ["net9.0", "net8.0", "netstandard2.0", "net462"],
            "Authors",
            "LICENSE",
            "icon.png");
        projectMetadataReader.ReadAsync(Arg.Any<FilePath>(), Arg.Any<CancellationToken>())
            .Returns(metadataResult);

        var packageOutputValidator = Substitute.For<IPackageOutputValidator>();
        packageOutputValidator
            .ValidateAsync(Arg.Any<PackageFamilyConfig>(), Arg.Any<PackageArtifacts>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<ProjectMetadata>())
            .Returns(PackageValidationResult.Pass(new PackageValidation([])));

        var runner = new PackageTaskRunner(
            repo.CakeContext,
            repo.CakeContext.Log,
            repo.Paths,
            new DotNetBuildConfiguration("Release"),
            new PackageBuildConfiguration(["sdl2-core"], "1.2.3"),
            packageFamilySelector,
            packageVersionResolver,
            dotNetPackInvoker,
            projectMetadataReader,
            packageOutputValidator);

        await runner.RunAsync();

        // Step 1: Pack native — standard `dotnet pack`, native csproj receives $(NativePayloadSource).
        dotNetPackInvoker.Received(1).Pack(
            Arg.Is<FilePath>(path => string.Equals(path.FullPath, repo.ResolveFile(family.NativeProject!).FullPath, StringComparison.OrdinalIgnoreCase)),
            Arg.Is<DotNetPackInvocation>(invocation =>
                string.Equals(invocation.Version, "1.2.3", StringComparison.Ordinal) &&
                invocation.NativePayloadSource != null &&
                string.Equals(invocation.NativePayloadSource.FullPath, repo.Paths.HarvestOutput.Combine(family.LibraryRef).FullPath, StringComparison.OrdinalIgnoreCase)),
            noRestore: false,
            noBuild: false);

        // Step 2: Pack managed — standard `dotnet pack`, no $(NativePayloadSource) threading.
        // Managed's ProjectReference to the native emits as a standard minimum-range dependency
        // (post-S1 SkiaSharp-style; no exact-pin CPM plumbing).
        dotNetPackInvoker.Received(1).Pack(
            Arg.Is<FilePath>(path => string.Equals(path.FullPath, repo.ResolveFile(family.ManagedProject!).FullPath, StringComparison.OrdinalIgnoreCase)),
            Arg.Is<DotNetPackInvocation>(invocation =>
                string.Equals(invocation.Version, "1.2.3", StringComparison.Ordinal) &&
                invocation.NativePayloadSource == null),
            noRestore: false,
            noBuild: false);
    }

    private static HarvestManifest CreateHarvestManifest(string libraryName)
    {
        return new HarvestManifest
        {
            LibraryName = libraryName,
            GeneratedTimestamp = DateTimeOffset.UtcNow,
            Rids =
            [
                new RidHarvestStatus
                {
                    LibraryName = libraryName,
                    Rid = "win-x64",
                    Triplet = "x64-windows-hybrid",
                    Success = true,
                    Timestamp = DateTimeOffset.UtcNow,
                    Statistics = new HarvestStatistics
                    {
                        PrimaryFilesCount = 1,
                        RuntimeFilesCount = 1,
                        LicenseFilesCount = 1,
                        DeployedPackagesCount = 1,
                        FilteredPackagesCount = 0,
                        DeploymentStrategy = "hybrid-static",
                    },
                },
            ],
            Summary = new HarvestSummary
            {
                TotalRids = 1,
                SuccessfulRids = 1,
                FailedRids = 0,
                SuccessRate = 1,
            },
        };
    }
}
