#pragma warning disable CA1031

using Build.Features.Ci;
using Build.Features.Coverage;
using Build.Features.DependencyAnalysis;
using Build.Features.Diagnostics;
using Build.Features.Harvesting;
using Build.Features.Info;
using Build.Features.LocalDev;
using Build.Features.Maintenance;
using Build.Features.Packaging;
using Build.Features.Preflight;
using Build.Features.Publishing;
using Build.Features.Vcpkg;
using Build.Features.Versioning;
using Build.Tests.Fixtures;
using Microsoft.Extensions.DependencyInjection;

namespace Build.Tests.Unit.CompositionRoot;

/// <summary>
/// Per-feature DI smoke per phase-x §10.6 + §14.3 sub-step 13.7. Each test seeds a
/// <see cref="ServiceCollection"/> with <see cref="TestHostFixture.AddTestHostBuildingBlocks"/>
/// (Cake fakes + Host singletons + Tool/Integration substitutes), invokes a single
/// <c>AddXFeature()</c>, captures the descriptors the feature added, builds the provider,
/// and asserts every added service type resolves without throwing.
/// <para>
/// Catches DI graph regressions (missing transitive dependency, mistyped factory closure,
/// wrong lifetime) at CI gate time without requiring full Cake host bootstrapping. Each
/// feature has exactly one smoke; future features add one each per ADR-004 §2.12 vertical
/// slice convention.
/// </para>
/// </summary>
public sealed class ServiceCollectionExtensionsSmokeTests
{
    [Test]
    public async Task AddInfoFeature_Should_Register_All_Pipeline_And_Validator_Types()
    {
        await AssertAllRegisteredTypesResolve(services => services.AddInfoFeature());
    }

    [Test]
    public async Task AddMaintenanceFeature_Should_Register_All_Pipeline_And_Validator_Types()
    {
        await AssertAllRegisteredTypesResolve(services => services.AddMaintenanceFeature());
    }

    [Test]
    public async Task AddCiFeature_Should_Register_All_Pipeline_And_Validator_Types()
    {
        await AssertAllRegisteredTypesResolve(services => services.AddCiFeature());
    }

    [Test]
    public async Task AddCoverageFeature_Should_Register_All_Pipeline_And_Validator_Types()
    {
        await AssertAllRegisteredTypesResolve(services => services.AddCoverageFeature());
    }

    [Test]
    public async Task AddVersioningFeature_Should_Register_All_Pipeline_And_Validator_Types()
    {
        // Versioning's IPackageVersionProvider factory closure consumes
        // IUpstreamVersionAlignmentValidator (registered by Preflight, which transitively
        // requires Packaging for IG58CrossFamilyDepResolvabilityValidator). Pre-register
        // both upstream features to mirror production where everything lands before
        // BuildServiceProvider — the smoke validates the resolved cross-feature contract.
        await AssertAllRegisteredTypesResolve(services =>
        {
            services.AddPackagingFeature("local");
            services.AddPreflightFeature();
            services.AddVersioningFeature();
        });
    }

    [Test]
    public async Task AddVcpkgFeature_Should_Register_All_Pipeline_And_Validator_Types()
    {
        await AssertAllRegisteredTypesResolve(services => services.AddVcpkgFeature());
    }

    [Test]
    public async Task AddDiagnosticsFeature_Should_Register_All_Pipeline_And_Validator_Types()
    {
        await AssertAllRegisteredTypesResolve(services => services.AddDiagnosticsFeature());
    }

    [Test]
    public async Task AddDependencyAnalysisFeature_Should_Register_All_Pipeline_And_Validator_Types()
    {
        await AssertAllRegisteredTypesResolve(services => services.AddDependencyAnalysisFeature());
    }

    [Test]
    public async Task AddPreflightFeature_Should_Register_All_Pipeline_And_Validator_Types()
    {
        // PreflightPipeline injects IG58CrossFamilyDepResolvabilityValidator
        // (registered by AddPackagingFeature).
        await AssertAllRegisteredTypesResolve(services =>
        {
            services.AddPackagingFeature("local");
            services.AddPreflightFeature();
        });
    }

    [Test]
    public async Task AddHarvestingFeature_Should_Register_All_Pipeline_And_Validator_Types()
    {
        // HarvestPipeline injects IDependencyPolicyValidator (registered by Packaging via
        // DependencyPolicyValidatorFactory). The factory transitively requires IStrategyResolver
        // (registered by Preflight). Both upstream features pre-registered to mirror
        // production composition.
        await AssertAllRegisteredTypesResolve(services =>
        {
            services.AddPackagingFeature("local");
            services.AddPreflightFeature();
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
        // Packaging depends on Preflight (PackagePipeline transitively) + Versioning
        // (IPackageVersionProvider used downstream). Pre-register them so the smoke
        // mirrors the production composition order.
        await AssertAllRegisteredTypesResolve(services =>
        {
            services.AddPreflightFeature();
            services.AddVersioningFeature();
            services.AddPackagingFeature("local");
        });
    }

    [Test]
    public async Task AddLocalDevFeature_Should_Register_All_Pipeline_And_Validator_Types()
    {
        // LocalDev orchestration feature consumes sibling pipelines (Preflight, Vcpkg,
        // Harvesting, Packaging, Versioning). Per ADR-004 §2.5 + §2.13 invariant #4
        // allowlist, it is registered last so all sibling pipelines are already in the
        // container; the smoke mirrors that ordering.
        await AssertAllRegisteredTypesResolve(services =>
        {
            services.AddPreflightFeature();
            services.AddVcpkgFeature();
            services.AddHarvestingFeature();
            services.AddVersioningFeature();
            services.AddPackagingFeature("local");
            services.AddLocalDevFeature();
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
