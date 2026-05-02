using Build.Features.Harvesting;
using Build.Features.LocalDev;
using Build.Features.Packaging;
using Build.Features.Packaging.ArtifactSourceResolvers;
using Build.Features.Preflight;
using Build.Features.Vcpkg;
using Build.Host;
using Build.Integrations.Vcpkg;
using Build.Shared.Harvesting;
using Build.Shared.Manifest;
using Build.Shared.Packaging;
using Build.Shared.Runtime;
using Build.Shared.Strategy;
using Build.Shared.Versioning;
using Build.Tests.Fixtures;
using Cake.Core.IO;
using Cake.Core.Tooling;
using Cake.Testing;
using NSubstitute;
using NuGet.Versioning;

namespace Build.Tests.Unit.Features.LocalDev;

/// <summary>
/// Post-B2 composition tests for <see cref="SetupLocalDevFlow"/>. The runner owns the
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

        var packagePipeline = Substitute.For<IPackagePipeline>();
        var runner = BuildRunner(repo, manifest, resolver, packagePipeline);

        await runner.RunAsync(repo.BuildContext);

        // Non-local profiles skip the local pipeline composition entirely; the resolver owns
        // the profile-specific feed-acquisition behavior.
        await resolver.Received(1).PrepareFeedAsync(
            Arg.Any<BuildContext>(),
            Arg.Is<IReadOnlyDictionary<string, NuGetVersion>>(versions => versions.Count == 0),
            Arg.Any<CancellationToken>());
        await resolver.Received(1).WriteConsumerOverrideAsync(
            Arg.Any<BuildContext>(),
            Arg.Is<IReadOnlyDictionary<string, NuGetVersion>>(versions => versions.Count == 0),
            Arg.Any<CancellationToken>());

        await packagePipeline.DidNotReceiveWithAnyArgs()
            .RunAsync(Arg.Any<PackRequest>(), Arg.Any<CancellationToken>());
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

        var packagePipeline = Substitute.For<IPackagePipeline>();
        packagePipeline.RunAsync(Arg.Any<PackRequest>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        var runner = BuildRunner(repo, manifest, resolver, packagePipeline);

        await runner.RunAsync(repo.BuildContext);

        // Pack received a non-empty mapping whose scope == concrete families in manifest.
        await packagePipeline.Received(1).RunAsync(
            Arg.Is<PackRequest>(request => request.Versions.Count == concreteFamilies.Count),
            Arg.Any<CancellationToken>());

        // Resolver received the same mapping the Pack stage consumed.
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

        // Harvest pipeline actually ran against the fake repo (real HarvestPipeline +
        // real ConsolidateHarvestPipeline) — receipt + consolidated licenses should exist.
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
            var passedRequest = packagePipeline.ReceivedCalls()
                .Select(call => call.GetArguments().OfType<PackRequest>().FirstOrDefault())
                .First(request => request is not null)!;
            var version = passedRequest.Versions[family.Name].ToNormalizedString();
            await Assert.That(version).StartsWith(expectedPrefix);
            observedTokens.Add(version[expectedPrefix.Length..]);
        }

        await Assert.That(observedTokens.Count).IsEqualTo(1);
    }

    [Test]
    public async Task RunAsync_Should_Write_VersionsJson_When_Profile_Is_Local()
    {
        var manifest = ManifestFixture.CreateTestManifestConfig();
        var concreteFamilies = manifest.PackageFamilies
            .Where(family => !string.IsNullOrWhiteSpace(family.ManagedProject) && !string.IsNullOrWhiteSpace(family.NativeProject))
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

        var packagePipeline = Substitute.For<IPackagePipeline>();
        packagePipeline.RunAsync(Arg.Any<PackRequest>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        var runner = BuildRunner(repo, manifest, resolver, packagePipeline);

        await runner.RunAsync(repo.BuildContext);

        // SetupLocalDev (C.11a) emits versions.json at the same path ResolveVersionsPipeline uses.
        var versionsJsonPath = repo.Paths.GetResolveVersionsOutputFile();
        var versionsFile = repo.FileSystem.GetFile(versionsJsonPath);
        await Assert.That(versionsFile.Exists).IsTrue();

        // Read and validate the shape: flat sorted dict { "sdl2-core": "<version>", ... }
        using var stream = versionsFile.Open(FileMode.Open, FileAccess.Read, FileShare.Read);
        using var reader = new StreamReader(stream);
        var json = await reader.ReadToEndAsync();
        var doc = System.Text.Json.JsonDocument.Parse(json);
        var root = doc.RootElement;

        await Assert.That(root.ValueKind).IsEqualTo(System.Text.Json.JsonValueKind.Object);

        var familyNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var property in root.EnumerateObject())
        {
            familyNames.Add(property.Name);
            await Assert.That(property.Value.GetString()).IsNotNull();
        }

        // The emitted mapping covers exactly the concrete families in manifest.
        await Assert.That(familyNames.Count).IsEqualTo(concreteFamilies.Count);
        foreach (var family in concreteFamilies)
        {
            await Assert.That(familyNames.Contains(family.Name)).IsTrue();
        }
    }

    [Test]
    public async Task RunAsync_Should_Not_Write_VersionsJson_When_Profile_Is_RemoteInternal()
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

        var packagePipeline = Substitute.For<IPackagePipeline>();
        var runner = BuildRunner(repo, manifest, resolver, packagePipeline);

        await runner.RunAsync(repo.BuildContext);

        // Non-local profiles skip the pipeline entirely — no versions.json should be written.
        var versionsJsonPath = repo.Paths.GetResolveVersionsOutputFile();
        var versionsFile = repo.FileSystem.GetFile(versionsJsonPath);
        await Assert.That(versionsFile.Exists).IsFalse();
    }

    private static SetupLocalDevFlow BuildRunner(
        FakeRepoHandles repo,
        ManifestConfig manifest,
        IArtifactSourceResolver resolver,
        IPackagePipeline packagePipeline)
    {
        SeedRunnerPrerequisites(repo);
        ConfigureSuccessfulTooling(repo);

        var runtimeProfile = CreateRuntimeProfile();
        var preflightPipeline = CreatePassingPreflightPipeline(repo, manifest);
        var ensureVcpkgDependenciesPipeline = new EnsureVcpkgDependenciesPipeline(
            new VcpkgBootstrapTool(repo.CakeContext),
            runtimeProfile,
            repo.CakeContext.Log);
        var harvestPipeline = CreateHarvestPipeline(repo, manifest, runtimeProfile);
        var consolidateHarvestPipeline = new ConsolidateHarvestPipeline(repo.CakeContext, new FakeLog(), repo.Paths);

        return new SetupLocalDevFlow(
            repo.CakeContext,
            repo.CakeContext.Log,
            repo.Paths,
            manifest,
            runtimeProfile,
            resolver,
            preflightPipeline,
            ensureVcpkgDependenciesPipeline,
            harvestPipeline,
            consolidateHarvestPipeline,
            packagePipeline);
    }

    private static PreflightPipeline CreatePassingPreflightPipeline(FakeRepoHandles repo, ManifestConfig manifest)
    {
        var vcpkgManifestReader = Substitute.For<IVcpkgManifestReader>();
        vcpkgManifestReader.ParseFile(Arg.Any<FilePath>()).Returns(CreateVcpkgManifest());

        var strategyCoherenceValidator = new StrategyCoherenceValidator(new StrategyResolver());

        var upstreamVersionAlignmentValidator = Substitute.For<IUpstreamVersionAlignmentValidator>();
        upstreamVersionAlignmentValidator.Validate(Arg.Any<ManifestConfig>(), Arg.Any<IReadOnlyDictionary<string, NuGetVersion>>())
            .Returns(UpstreamVersionAlignmentResult.Pass(new UpstreamVersionAlignmentValidation([])));

        var csprojPackContractValidator = Substitute.For<ICsprojPackContractValidator>();
        csprojPackContractValidator.Validate(Arg.Any<ManifestConfig>(), Arg.Any<DirectoryPath>())
            .Returns(CsprojPackContractResult.Pass(new CsprojPackContractValidation([])));

        return new PreflightPipeline(
            manifest,
            vcpkgManifestReader,
            strategyCoherenceValidator,
            upstreamVersionAlignmentValidator,
            csprojPackContractValidator,
            new G58CrossFamilyDepResolvabilityValidator(),
            new PreflightReporter(repo.CakeContext),
            repo.CakeContext,
            repo.CakeContext.Log,
            repo.Paths);
    }

    private static HarvestPipeline CreateHarvestPipeline(
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
                new HashSet<string>(StringComparer.Ordinal) { sourcePrimary.FullPath },
                [new BinaryNode(sourcePrimary.FullPath, "sdl2", "sdl2")],
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

        return new HarvestPipeline(
            binaryClosureWalker,
            artifactPlanner,
            artifactDeployer,
            dependencyPolicyValidator,
            runtimeProfile,
            manifest,
            repo.CakeContext,
            new FakeLog(),
            repo.Paths);
    }

    private static IRuntimeProfile CreateRuntimeProfile()
    {
        var runtimeProfile = Substitute.For<IRuntimeProfile>();
        runtimeProfile.Rid.Returns(Rid);
        runtimeProfile.Triplet.Returns(Triplet);
        runtimeProfile.Family.Returns(RuntimeFamily.Windows);
        runtimeProfile.IsSystemFile(Arg.Any<string>()).Returns(false);
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
