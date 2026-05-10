#pragma warning disable CA1031

using Build.Data;
using Build.Targets.ConsolidateHarvest;
using Build.Targets.Harvest;
using Build.Targets.NativeSmoke;
using Build.Targets.PackageConsumerSmoke;
using Build.Targets.PreFlightCheck;
using Build.Targets.Package;
using Build.Targets.PublishStaging;
using Build.Tests.Fixtures;
using Build.Validation;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Spectre.Console;

namespace Build.Tests.Unit.CompositionRoot;

/// <summary>
/// Per-feature DI smoke. Each test seeds a
/// <see cref="ServiceCollection"/> with <see cref="TestHostFixture.AddTestHostBuildingBlocks"/>
/// (Cake fakes + Host singletons + Tool/Integration substitutes), invokes a single
/// <c>AddXFeature()</c>, captures the descriptors the feature added, builds the provider,
/// and asserts every added service type resolves without throwing.
/// <para>
/// Catches DI graph regressions (missing transitive dependency, mistyped factory closure,
/// wrong lifetime) at CI gate time without requiring full Cake host bootstrapping. Each
/// feature has exactly one smoke; future features add one each per the vertical
/// slice convention.
/// </para>
/// <para>
/// <b>V1 fixture deferral:</b> this file consumes V1 <c>TestHostFixture.AddTestHostBuildingBlocks</c>;
/// the testing-guidelines V2-on-touch rule is intentionally deferred for this file because
/// creating a V2 equivalent (<c>FakeCakeWorldV2</c>-derived <c>IServiceCollection</c> seed) is
/// its own infra slice rather than a single-test migration. Tracked in
/// <c>docs/parking-lot.md</c> "S14 P8 Reviewer Follow-Ups".
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
            services.AddValidators();
            services.AddPreFlightCheck();
        });
    }

    [Test]
    public async Task AddHarvest_Should_Register_All_Collaborator_Types()
    {
        // HarvestTask injects walker/planner/deployer/preconditions validators registered by
        // AddValidators(), the rid-status repository registered by AddData() per the
        // repository-cohort rule, and ManifestConfig + IRuntimeScanner from AddTestHostBuildingBlocks.
        // Vcpkg package metadata is read through Cake Vcpkg aliases on ICakeContext.
        // HarvestReporter takes IAnsiConsole — Program.cs binds the real console; smoke tests bind
        // a substitute so the resolution graph closes.
        await AssertAllRegisteredTypesResolve(services =>
        {
            services.AddSingleton(Substitute.For<IAnsiConsole>());
            services.AddData();
            services.AddValidators();
            services.AddHarvest();
        });
    }

    [Test]
    public async Task AddNativeSmoke_Should_Register_All_Collaborator_Types()
    {
        // AddNativeSmoke registers IMsvcDevEnvironment only after Phase 5 inline. Task pulls
        // INativeSmokePreconditionsValidator from AddValidators(); other deps from host blocks.
        await AssertAllRegisteredTypesResolve(services =>
        {
            services.AddValidators();
            services.AddNativeSmoke();
        });
    }

    [Test]
    public async Task AddConsolidateHarvest_Should_Register_All_Collaborator_Types()
    {
        // ConsolidateHarvestReporter takes IAnsiConsole — see AddHarvest smoke for rationale.
        await AssertAllRegisteredTypesResolve(services =>
        {
            services.AddSingleton(Substitute.For<IAnsiConsole>());
            services.AddConsolidateHarvest();
        });
    }

    [Test]
    public async Task AddPublishStaging_Should_Register_All_Collaborator_Types()
    {
        // PublishStagingTask is discovered by Cake; AddPublishStaging registers
        // INuGetFeedClient itself post-P10 (relocated from the retired AddIntegrations group).
        await AssertAllRegisteredTypesResolve(services => services.AddPublishStaging());
    }

    [Test]
    public async Task AddPackageConsumerSmoke_Should_Register_All_Collaborator_Types()
    {
        // PackageConsumerSmokeTask injects DotNetSmokeRunner + MonoAvailabilityProbe +
        // PackageConsumerSmokeReporter + IDotNetRuntimeEnvironment (all registered by
        // AddPackageConsumerSmoke post-P10) + IPackageConsumerSmokePreconditionsValidator
        // (registered by AddValidators) + IProjectMetadataReader (registered by AddPackage,
        // root Build.Packaging cross-target reader). PackageConsumerSmokeReporter takes
        // IAnsiConsole — bind a substitute so the resolution graph closes.
        await AssertAllRegisteredTypesResolve(services =>
        {
            services.AddSingleton(Substitute.For<IAnsiConsole>());
            services.AddValidators();
            services.AddPackage();
            services.AddPackageConsumerSmoke();
        });
    }

    private static async Task AssertAllRegisteredTypesResolve(Action<IServiceCollection> register)
    {
        ArgumentNullException.ThrowIfNull(register);

        var services = new ServiceCollection().AddTestHostBuildingBlocks();
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
