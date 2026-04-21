using System.Collections.Immutable;
using System.IO;
using System.Xml.Linq;
using Build.Application.Harvesting;
using Build.Application.Packaging;
using Build.Application.Preflight;
using Build.Application.Vcpkg;
using Build.Context;
using Build.Context.Configs;
using Build.Context.Models;
using Build.Domain.Harvesting.Models;
using Build.Domain.Harvesting.Results;
using Build.Domain.Packaging.Models;
using Build.Domain.Preflight;
using Build.Domain.Preflight.Models;
using Build.Domain.Preflight.Results;
using Build.Domain.Runtime;
using Build.Domain.Strategy;
using Build.Domain.Strategy.Results;
using Build.Infrastructure.Tools.Vcpkg;
using Build.Infrastructure.Vcpkg;
using Build.Tests.Fixtures;
using Cake.Core;
using Cake.Core.IO;
using Cake.Core.Tooling;
using Cake.Testing;
using NSubstitute;
using NuGet.Versioning;

namespace Build.Tests.Unit.Application.Packaging;

/// <summary>
/// Post-B2 composition tests for <see cref="SetupLocalDevTaskRunner"/>. The runner owns the
/// <c>Preflight → EnsureVcpkg → Harvest → ConsolidateHarvest → Pack → resolver-handoff</c>
/// pipeline for the <see cref="ArtifactProfile.Local"/> profile; non-local profiles delegate
/// straight to <see cref="IArtifactSourceResolver"/>. NativeSmoke is intentionally absent
/// (it would force CMake + MSVC Developer shell on contributors who only touch managed
/// bindings).
/// </summary>
public sealed class SetupLocalDevTaskRunnerTests
{
    private const string Rid = "win-x64";
    private const string Triplet = "x64-windows-hybrid";

    [Test]
    public async Task RunAsync_Should_Delegate_To_Resolver_When_Profile_Is_RemoteInternal()
    {
        var manifest = ManifestFixture.CreateTestManifestConfig();
        var repo = new FakeRepoBuilder(FakeRepoPlatform.Windows)
            .WithManifest(manifest)
            .WithVcpkgJson(CreateVcpkgManifest())
            .WithRid(Rid)
            .WithLibraries("SDL2")
            .BuildContextWithHandles();

        var resolver = Substitute.For<IArtifactSourceResolver>();
        resolver.Profile.Returns(ArtifactProfile.RemoteInternal);
        resolver.LocalFeedPath.Returns(repo.Paths.PackagesOutput);
        resolver.PrepareFeedAsync(
                Arg.Any<BuildContext>(),
                Arg.Any<IReadOnlyDictionary<string, NuGetVersion>>(),
                Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);
        resolver.WriteConsumerOverrideAsync(
                Arg.Any<BuildContext>(),
                Arg.Any<IReadOnlyDictionary<string, NuGetVersion>>(),
                Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        var packageTaskRunner = Substitute.For<IPackageTaskRunner>();
        var runner = BuildRunner(repo, manifest, resolver, packageTaskRunner);

        await runner.RunAsync(repo.BuildContext);

        // Non-local profile skips the pipeline composition entirely; the resolver is the
        // single point where profile-specific feed acquisition would land in Phase 2b.
        await resolver.Received(1).PrepareFeedAsync(
            Arg.Any<BuildContext>(),
            Arg.Is<IReadOnlyDictionary<string, NuGetVersion>>(versions => versions.Count == 0),
            Arg.Any<CancellationToken>());
        await resolver.Received(1).WriteConsumerOverrideAsync(
            Arg.Any<BuildContext>(),
            Arg.Is<IReadOnlyDictionary<string, NuGetVersion>>(versions => versions.Count == 0),
            Arg.Any<CancellationToken>());

        await packageTaskRunner.DidNotReceiveWithAnyArgs()
            .RunAsync(Arg.Any<PackageBuildConfiguration>(), Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task RunAsync_Should_Compose_Full_Pipeline_And_Hand_Mapping_To_Resolver_When_Profile_Is_Local()
    {
        var manifest = ManifestFixture.CreateTestManifestConfig();
        var concreteFamilies = manifest.PackageFamilies
            .Where(family => !string.IsNullOrWhiteSpace(family.ManagedProject) && !string.IsNullOrWhiteSpace(family.NativeProject))
            .OrderBy(family => family.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var repo = new FakeRepoBuilder(FakeRepoPlatform.Windows)
            .WithManifest(manifest)
            .WithVcpkgJson(CreateVcpkgManifest())
            .WithRid(Rid)
            .WithLibraries("SDL2")
            .BuildContextWithHandles();

        var resolver = Substitute.For<IArtifactSourceResolver>();
        resolver.Profile.Returns(ArtifactProfile.Local);
        resolver.LocalFeedPath.Returns(repo.Paths.PackagesOutput);
        resolver.PrepareFeedAsync(
                Arg.Any<BuildContext>(),
                Arg.Any<IReadOnlyDictionary<string, NuGetVersion>>(),
                Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);
        resolver.WriteConsumerOverrideAsync(
                Arg.Any<BuildContext>(),
                Arg.Any<IReadOnlyDictionary<string, NuGetVersion>>(),
                Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        var packageTaskRunner = Substitute.For<IPackageTaskRunner>();
        packageTaskRunner.RunAsync(Arg.Any<PackageBuildConfiguration>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        var runner = BuildRunner(repo, manifest, resolver, packageTaskRunner);

        await runner.RunAsync(repo.BuildContext);

        // Pack received a non-empty mapping whose scope == concrete families in manifest.
        await packageTaskRunner.Received(1).RunAsync(
            Arg.Is<PackageBuildConfiguration>(config => config.ExplicitVersions.Count == concreteFamilies.Count),
            Arg.Any<CancellationToken>());

        // Resolver received the same mapping the Pack stage consumed — ADR-003 §2.4
        // resolve-once + distribute-immutably invariant.
        await resolver.Received(1).PrepareFeedAsync(
            Arg.Any<BuildContext>(),
            Arg.Is<IReadOnlyDictionary<string, NuGetVersion>>(versions =>
                versions.Count == concreteFamilies.Count),
            Arg.Any<CancellationToken>());
        await resolver.Received(1).WriteConsumerOverrideAsync(
            Arg.Any<BuildContext>(),
            Arg.Is<IReadOnlyDictionary<string, NuGetVersion>>(versions =>
                versions.Count == concreteFamilies.Count),
            Arg.Any<CancellationToken>());

        // Harvest pipeline actually ran against the fake repo (real HarvestTaskRunner +
        // real ConsolidateHarvestTaskRunner) — receipt + consolidated licenses should exist.
        await Assert.That(repo.Exists("artifacts/harvest_output/SDL2/harvest-manifest.json")).IsTrue();
        var consolidatedLicenseDir = repo.FileSystem
            .GetDirectory(repo.Paths.GetHarvestLibraryConsolidatedLicensesDir("SDL2"));
        await Assert.That(consolidatedLicenseDir.Exists).IsTrue();

        // Mapping suffix discipline: each resolved version carries the local.<timestamp>
        // token and starts at <upstream-major>.<upstream-minor>.0-local per ManifestVersionProvider.
        var observedTokens = new HashSet<string>(StringComparer.Ordinal);
        foreach (var family in concreteFamilies)
        {
            var library = manifest.LibraryManifests.Single(candidate =>
                string.Equals(candidate.Name, family.LibraryRef, StringComparison.OrdinalIgnoreCase));
            NuGetVersion.TryParse(library.VcpkgVersion, out var upstream);

            var expectedPrefix = $"{upstream!.Major}.{upstream.Minor}.0-local.";
            var passedMapping = packageTaskRunner.ReceivedCalls()
                .Select(call => call.GetArguments().OfType<PackageBuildConfiguration>().FirstOrDefault())
                .First(config => config is not null)!;
            var version = passedMapping.ExplicitVersions[family.Name].ToNormalizedString();
            await Assert.That(version).StartsWith(expectedPrefix);
            observedTokens.Add(version[expectedPrefix.Length..]);
        }

        await Assert.That(observedTokens.Count).IsEqualTo(1);
    }

    private static SetupLocalDevTaskRunner BuildRunner(
        FakeRepoHandles repo,
        ManifestConfig manifest,
        IArtifactSourceResolver resolver,
        IPackageTaskRunner packageTaskRunner)
    {
        SeedRunnerPrerequisites(repo);
        ConfigureSuccessfulTooling(repo);

        var runtimeProfile = CreateRuntimeProfile();
        var preflightTaskRunner = CreatePassingPreflightTaskRunner(repo, manifest);
        var ensureVcpkgDependenciesTaskRunner = new EnsureVcpkgDependenciesTaskRunner(
            new VcpkgBootstrapTool(repo.CakeContext),
            runtimeProfile,
            repo.CakeContext.Log);
        var harvestTaskRunner = CreateHarvestTaskRunner(repo, manifest, runtimeProfile);
        var consolidateHarvestTaskRunner = new ConsolidateHarvestTaskRunner();

        return new SetupLocalDevTaskRunner(
            repo.CakeContext.Log,
            manifest,
            resolver,
            preflightTaskRunner,
            ensureVcpkgDependenciesTaskRunner,
            harvestTaskRunner,
            consolidateHarvestTaskRunner,
            packageTaskRunner);
    }

    private static PreflightTaskRunner CreatePassingPreflightTaskRunner(FakeRepoHandles repo, ManifestConfig manifest)
    {
        var vcpkgManifestReader = Substitute.For<IVcpkgManifestReader>();
        vcpkgManifestReader.ParseFile(Arg.Any<FilePath>()).Returns(CreateVcpkgManifest());

        var versionConsistencyValidator = Substitute.For<IVersionConsistencyValidator>();
        versionConsistencyValidator.Validate(Arg.Any<ManifestConfig>(), Arg.Any<VcpkgManifest>(), Arg.Any<FilePath>(), Arg.Any<FilePath>())
            .Returns(VersionConsistencyResult.Pass(new VersionConsistencyValidation(
                repo.ResolveFile("build/manifest.json"),
                repo.ResolveFile("vcpkg.json"),
                [])));

        var strategyCoherenceValidator = Substitute.For<IStrategyCoherenceValidator>();
        strategyCoherenceValidator.Validate(Arg.Any<IImmutableList<RuntimeInfo>>())
            .Returns(StrategyCoherenceResult.Pass(new StrategyCoherenceValidation([])));

        var coreLibraryIdentityValidator = Substitute.For<ICoreLibraryIdentityValidator>();
        coreLibraryIdentityValidator.Validate(Arg.Any<ManifestConfig>())
            .Returns(CoreLibraryIdentityResult.Pass(new CoreLibraryIdentityValidation(
                new CoreLibraryIdentityCheck(
                    ManifestCoreVcpkgName: "sdl2",
                    PackagingConfigCoreLibrary: manifest.PackagingConfig.CoreLibrary,
                    CoreLibraryManifestCount: 1,
                    Status: CoreLibraryIdentityCheckStatus.Match,
                    ErrorMessage: null))));

        var upstreamVersionAlignmentValidator = Substitute.For<IUpstreamVersionAlignmentValidator>();
        upstreamVersionAlignmentValidator.Validate(Arg.Any<ManifestConfig>(), Arg.Any<IReadOnlyDictionary<string, NuGetVersion>>())
            .Returns(UpstreamVersionAlignmentResult.Pass(new UpstreamVersionAlignmentValidation([])));

        var csprojPackContractValidator = Substitute.For<ICsprojPackContractValidator>();
        csprojPackContractValidator.Validate(Arg.Any<ManifestConfig>(), Arg.Any<DirectoryPath>())
            .Returns(CsprojPackContractResult.Pass(new CsprojPackContractValidation([])));

        return new PreflightTaskRunner(
            manifest,
            new PackageBuildConfiguration(new Dictionary<string, NuGetVersion>(StringComparer.OrdinalIgnoreCase)),
            vcpkgManifestReader,
            versionConsistencyValidator,
            strategyCoherenceValidator,
            coreLibraryIdentityValidator,
            upstreamVersionAlignmentValidator,
            csprojPackContractValidator,
            new PreflightReporter(repo.CakeContext));
    }

    private static HarvestTaskRunner CreateHarvestTaskRunner(
        FakeRepoHandles repo,
        ManifestConfig manifest,
        IRuntimeProfile runtimeProfile)
    {
        var sourcePrimary = repo.ResolveFile($"vcpkg_installed/{Triplet}/bin/SDL2.dll");
        var deployedPrimary = repo.ResolveFile($"artifacts/harvest_output/SDL2/runtimes/{Rid}/native/SDL2.dll");
        var deployedLicense = repo.ResolveFile($"artifacts/harvest_output/SDL2/licenses/{Rid}/sdl2/copyright");

        var binaryClosureWalker = Substitute.For<IBinaryClosureWalker>();
        binaryClosureWalker.BuildClosureAsync(Arg.Any<LibraryManifest>(), Arg.Any<CancellationToken>())
            .Returns(new BinaryClosure(
                new HashSet<FilePath> { sourcePrimary },
                [new BinaryNode(sourcePrimary, "sdl2", "sdl2")],
                new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "sdl2" }));

        var artifactPlanner = Substitute.For<IArtifactPlanner>();
        artifactPlanner.CreatePlanAsync(Arg.Any<LibraryManifest>(), Arg.Any<BinaryClosure>(), Arg.Any<DirectoryPath>(), Arg.Any<CancellationToken>())
            .Returns(new DeploymentPlan(
                Array.Empty<DeploymentAction>(),
                new DeploymentStatistics(
                    LibraryName: "SDL2",
                    PrimaryFiles: [new FileDeploymentInfo(deployedPrimary, "sdl2", DeploymentLocation.FileSystem)],
                    RuntimeFiles: [],
                    LicenseFiles: [new FileDeploymentInfo(deployedLicense, "sdl2", DeploymentLocation.FileSystem)],
                    DeployedPackages: new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "sdl2" },
                    FilteredPackages: new HashSet<string>(StringComparer.OrdinalIgnoreCase),
                    DeploymentStrategy: DeploymentStrategy.DirectCopy)));

        var artifactDeployer = Substitute.For<IArtifactDeployer>();
        artifactDeployer.DeployArtifactsAsync(Arg.Any<DeploymentPlan>(), Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                WriteTextFile(repo, deployedPrimary, "payload");
                WriteTextFile(repo, deployedLicense, "license");
                return CopierResult.ToSuccess();
            });

        var dependencyPolicyValidator = Substitute.For<IDependencyPolicyValidator>();
        dependencyPolicyValidator.Validate(Arg.Any<BinaryClosure>(), Arg.Any<LibraryManifest>())
            .Returns(ValidationResult.Pass());

        return new HarvestTaskRunner(
            binaryClosureWalker,
            artifactPlanner,
            artifactDeployer,
            dependencyPolicyValidator,
            runtimeProfile,
            manifest);
    }

    private static IRuntimeProfile CreateRuntimeProfile()
    {
        var runtimeProfile = Substitute.For<IRuntimeProfile>();
        runtimeProfile.Rid.Returns(Rid);
        runtimeProfile.Triplet.Returns(Triplet);
        runtimeProfile.PlatformFamily.Returns(PlatformFamily.Windows);
        runtimeProfile.IsSystemFile(Arg.Any<FilePath>()).Returns(false);
        return runtimeProfile;
    }

    private static void ConfigureSuccessfulTooling(FakeRepoHandles repo)
    {
        var processRunner = Substitute.For<IProcessRunner>();
        processRunner.Start(Arg.Any<FilePath>(), Arg.Any<ProcessSettings>())
            .Returns(_ =>
            {
                var process = new FakeProcess();
                process.SetExitCode(0);
                process.SetStandardOutput(["ok"]);
                process.SetStandardError([]);
                return process;
            });

        var toolLocator = Substitute.For<IToolLocator>();
        toolLocator.Resolve(Arg.Any<string>()).Returns(repo.ResolveFile("external/vcpkg/vcpkg.exe"));
        toolLocator.Resolve(Arg.Any<IEnumerable<string>>()).Returns(repo.ResolveFile("external/vcpkg/vcpkg.exe"));

        repo.CakeContext.ProcessRunner.Returns(processRunner);
        repo.CakeContext.Tools.Returns(toolLocator);
    }

    private static void SeedRunnerPrerequisites(FakeRepoHandles repo)
    {
        WriteTextFile(repo, repo.ResolveFile("external/vcpkg/vcpkg.exe"), "vcpkg");
        WriteTextFile(repo, repo.ResolveFile("external/vcpkg/bootstrap-vcpkg.bat"), "@echo off");
        WriteTextFile(repo, repo.ResolveFile("external/vcpkg/bootstrap-vcpkg.sh"), "#!/usr/bin/env bash");
        WriteTextFile(repo, repo.ResolveFile($"vcpkg_installed/{Triplet}/bin/SDL2.dll"), "source-payload");
    }

    private static VcpkgManifest CreateVcpkgManifest()
    {
        return new VcpkgManifest
        {
            Overrides =
            [
                new VcpkgOverride
                {
                    Name = "sdl2",
                    Version = "2.32.10",
                    PortVersion = 0,
                },
                new VcpkgOverride
                {
                    Name = "sdl2-image",
                    Version = "2.8.8",
                    PortVersion = 2,
                },
            ],
        };
    }

    private static void WriteTextFile(FakeRepoHandles repo, FilePath path, string content)
    {
        var directory = repo.FileSystem.GetDirectory(path.GetDirectory());
        if (!directory.Exists)
        {
            directory.Create();
        }

        var file = repo.FileSystem.GetFile(path);
        using var stream = file.Open(FileMode.Create, FileAccess.Write, FileShare.None);
        using var writer = new StreamWriter(stream);
        writer.Write(content);
    }
}
