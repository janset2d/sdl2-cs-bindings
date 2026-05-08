#pragma warning disable CA1031

using Build.Features.Ci;
using Build.Features.Harvesting;
using Build.Features.Packaging;
using Build.Features.Publishing;
using Build.Features.Vcpkg;
using Build.Targets.PreFlightCheck;
using Build.Tests.Fixtures;
using Build.Validation;
using Microsoft.Extensions.DependencyInjection;

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
/// </summary>
public sealed class ServiceCollectionExtensionsSmokeTests
{
    [Test]
    public async Task AddCiFeature_Should_Register_All_Pipeline_And_Validator_Types()
    {
        await AssertAllRegisteredTypesResolve(services => services.AddCiFeature());
    }

    [Test]
    public async Task AddVcpkgFeature_Should_Register_All_Pipeline_And_Validator_Types()
    {
        await AssertAllRegisteredTypesResolve(services => services.AddVcpkgFeature());
    }

    [Test]
    public async Task AddPreFlightCheck_Should_Register_All_Reporter_And_Validator_Types()
    {
        // PreFlightCheckTask injects validators registered by AddValidators() (Validation/ root)
        // and repositories from AddRepositories(). AddPreFlightCheck only registers the
        // target-local PreflightReporter — Cake discovers the task class via [TaskName].
        await AssertAllRegisteredTypesResolve(services =>
        {
            services.AddValidators();
            services.AddPreFlightCheck();
        });
    }

    [Test]
    public async Task AddHarvestingFeature_Should_Register_All_Pipeline_And_Validator_Types()
    {
        // HarvestPipeline injects IDependencyPolicyValidator (registered by Packaging via
        // DependencyPolicyValidatorFactory). Packaging is pre-registered to mirror production
        // composition. AddValidators() supplies validators relocated to Validation/, which
        // Packaging consumes transitively.
        await AssertAllRegisteredTypesResolve(services =>
        {
            services.AddValidators();
            services.AddPackagingFeature();
            services.AddHarvestingFeature();
        });
    }

    [Test]
    public async Task AddPublishingFeature_Should_Register_All_Pipeline_And_Validator_Types()
    {
        await AssertAllRegisteredTypesResolve(services => services.AddPublishingFeature());
    }

    [Test]
    public async Task AddPackagingFeature_Should_Register_All_Pipeline_And_Validator_Types()
    {
        // AddValidators() supplies validators relocated to Validation/.
        await AssertAllRegisteredTypesResolve(services =>
        {
            services.AddValidators();
            services.AddPackagingFeature();
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
