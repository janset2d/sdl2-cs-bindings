#pragma warning disable CA1031

using Build.Data;
using Build.Targets.ConsolidateHarvest;
using Build.Targets.GenerateBindings;
using Build.Targets.Harvest;
using Build.Targets.NativeSmoke;
using Build.Targets.PackageConsumerSmoke;
using Build.Targets.PreFlightCheck;
using Build.Targets.Package;
using Build.Targets.PublishStaging;
using Build.Tests.Fixtures;
using Build.Validation;
using Microsoft.Extensions.DependencyInjection;

namespace Build.Tests.Unit.CompositionRoot;

/// <summary>
/// Per-feature DI smoke. Each test seeds a <see cref="ServiceCollection"/> with
/// <see cref="FakeCakeWorld"/> Cake primitives and build-host context, invokes a single
/// <c>AddX()</c>, captures the descriptors the target added, builds the provider, and
/// asserts every added service type resolves without throwing.
/// <para>
/// Catches DI graph regressions (missing transitive dependency, mistyped factory closure,
/// wrong lifetime) at CI gate time without requiring full Cake host bootstrapping. Each
/// feature has exactly one smoke; future features add one each per the vertical
/// slice convention.
/// </para>
/// </summary>
public sealed class ServiceCollectionExtensionsSmokeTests
{
    [Test]
    public async Task AddPreFlightCheck_Should_Register_All_Reporter_And_Validator_Types()
    {
        // PreFlightCheckTask injects validators registered by AddValidators() (Validation/ root)
        // and repositories from AddData(). AddPreFlightCheck only registers the
        // target-local PreflightReporter — Cake discovers the task class via [TaskName].
        await AssertAllRegisteredTypesResolve(services =>
        {
            services.AddData();
            services.AddValidators();
            services.AddPreFlightCheck();
        });
    }

    [Test]
    public async Task AddHarvest_Should_Register_All_Collaborator_Types()
    {
        // HarvestTask injects walker/planner/deployer/preconditions validators registered by
        // AddValidators(), the rid-status repository registered by AddData() per the
        // repository-cohort rule, and ManifestConfig + IRuntimeScanner from canonical composition.
        // Vcpkg package metadata is read through Cake Vcpkg aliases on ICakeContext.
        // HarvestReporter takes IAnsiConsole; FakeCakeWorld binds a TestConsole.
        await AssertAllRegisteredTypesResolve(services =>
        {
            services.AddData();
            services.AddValidators();
            services.AddHarvest();
        });
    }

    [Test]
    public async Task AddNativeSmoke_Should_Register_All_Collaborator_Types()
    {
        // AddNativeSmoke registers IMsvcDevEnvironment only after Phase 5 inline. Task pulls
        // INativeSmokePreconditionsValidator from AddValidators(); AddData closes validators
        // that read file-backed contracts, matching Program.cs composition order.
        await AssertAllRegisteredTypesResolve(services =>
        {
            services.AddData();
            services.AddValidators();
            services.AddNativeSmoke();
        });
    }

    [Test]
    public async Task AddConsolidateHarvest_Should_Register_All_Collaborator_Types()
    {
        // ConsolidateHarvestTask reads rid-status/manifest data through AddData repositories.
        // ConsolidateHarvestReporter takes IAnsiConsole; FakeCakeWorld binds a TestConsole.
        await AssertAllRegisteredTypesResolve(services =>
        {
            services.AddData();
            services.AddConsolidateHarvest();
        });
    }

    [Test]
    public async Task AddPublishStaging_Should_Register_All_Collaborator_Types()
    {
        // PublishStagingTask is discovered by Cake; AddPublishStaging registers
        // INuGetFeedClient itself and the publish-stage collaborators it owns.
        await AssertAllRegisteredTypesResolve(services => services.AddPublishStaging());
    }

    [Test]
    public async Task AddGenerateBindings_Should_Register_All_Collaborator_Types()
    {
        // GenerateBindingsTask is discovered by Cake via [TaskName]; AddGenerateBindings
        // registers the parser/header/validator collaborators that the task injects.
        // AddData() chains in IDynapiManifestRepository which GenerateBindingsTask also
        // consumes — Program.cs production order calls both, the smoke must mirror that
        // dependency to certify the feature module's real composition. PreviewEmitter
        // is a static utility — not registered.
        await AssertAllRegisteredTypesResolve(services =>
        {
            services.AddData();
            services.AddGenerateBindings();
        });
    }

    [Test]
    public async Task AddPackageConsumerSmoke_Should_Register_All_Collaborator_Types()
    {
        // PackageConsumerSmokeTask injects DotNetSmokeRunner + MonoAvailabilityProbe +
        // PackageConsumerSmokeReporter + IDotNetRuntimeEnvironment (all registered by
        // AddPackageConsumerSmoke) + IPackageConsumerSmokePreconditionsValidator
        // (registered by AddValidators) + IProjectMetadataReader (registered by AddPackage).
        // PackageConsumerSmokeReporter takes IAnsiConsole; FakeCakeWorld binds a TestConsole.
        await AssertAllRegisteredTypesResolve(services =>
        {
            services.AddData();
            services.AddValidators();
            services.AddPackage();
            services.AddPackageConsumerSmoke();
        });
    }

    private static async Task AssertAllRegisteredTypesResolve(Action<IServiceCollection> register)
    {
        ArgumentNullException.ThrowIfNull(register);

        var world = FakeCakeWorld.CreateWindows();
        var services = new ServiceCollection()
            .AddFakeCakeWorld(world);
        var hostDescriptorCount = services.Count;

        register(services);

        var addedDescriptors = services.Skip(hostDescriptorCount).ToList();

        using var provider = services.BuildServiceProvider();

        var unresolved = new List<string>();
        foreach (var descriptor in addedDescriptors)
        {
            try
            {
                var resolved = provider.GetService(descriptor.ServiceType);
                if (resolved is null)
                {
                    unresolved.Add(descriptor.ServiceType.FullName ?? descriptor.ServiceType.Name);
                }
            }
            catch (Exception ex)
            {
                unresolved.Add($"{descriptor.ServiceType.FullName ?? descriptor.ServiceType.Name} → {ex.GetType().Name}: {ex.Message}");
            }
        }

        await Assert.That(unresolved)
            .IsEmpty()
            .Because(unresolved.Count == 0
                ? "all feature-registered services resolved"
                : "unresolved service registrations:\n" + string.Join('\n', unresolved));
    }
}
