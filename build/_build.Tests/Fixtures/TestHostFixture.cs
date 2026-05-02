using Build.Host.Paths;
using Build.Integrations.Coverage;
using Build.Integrations.DependencyAnalysis;
using Build.Integrations.DotNet;
using Build.Integrations.NuGet;
using Build.Integrations.Vcpkg;
using Build.Shared.Manifest;
using Build.Shared.Runtime;
using Cake.Core;
using Cake.Core.Diagnostics;
using Cake.Core.IO;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;

namespace Build.Tests.Fixtures;

/// <summary>
/// Shared DI seam for per-feature <c>ServiceCollectionExtensions</c> smoke tests
/// (phase-x §10.6 + §14.3 sub-step 13.7). Registers every Cake fake, Host singleton,
/// Tool / Integration substitute that any feature transitively consumes — production
/// code path under <c>Program.cs ConfigureBuildServices</c> minus the per-feature
/// <c>AddXFeature()</c> calls. Tests then add a single feature on top and assert
/// the resulting <see cref="IServiceProvider"/> resolves every registered descriptor
/// without throwing.
/// </summary>
/// <remarks>
/// <para>
/// Cake fakes (<see cref="ICakeContext"/>, <see cref="ICakeLog"/>, <see cref="ICakeEnvironment"/>,
/// <see cref="IFileSystem"/>) come from <see cref="FakeRepoBuilder"/> so the smoke shares its
/// FakeFileSystem-backed shape with the rest of the test suite.
/// </para>
/// <para>
/// Tools / Integrations are NSubstitute-backed for interface registrations.
/// <see cref="VcpkgBootstrapTool"/> is sealed and is registered as a concrete singleton —
/// its constructor only requires <see cref="ICakeContext"/>, which the fixture provides.
/// Production registration lives in <see cref="Build.Integrations.ServiceCollectionExtensions.AddIntegrations"/>.
/// </para>
/// </remarks>
public static class TestHostFixture
{
    public static IServiceCollection AddTestHostBuildingBlocks(this IServiceCollection services, FakeRepoBuilder? repoBuilder = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        var builder = repoBuilder ?? new FakeRepoBuilder();
        var handles = builder.BuildContextWithHandles();

        var manifest = handles.BuildContext.Manifest;
        var options = handles.BuildContext.Options;
        var pathService = handles.Paths;
        var runtimeProfile = handles.BuildContext.Runtime;
        var cakeContext = handles.CakeContext;

        // Cake primitives — registered as both concrete and interface where the codebase
        // injects either shape. ICakeContext doubles as ICakeLog/ICakeEnvironment/IFileSystem
        // carrier for some classes that take it whole, and exposes them individually for
        // classes that take only the slice they need.
        services.AddSingleton(cakeContext);
        services.AddSingleton(cakeContext.Log);
        services.AddSingleton(cakeContext.Environment);
        services.AddSingleton(cakeContext.FileSystem);
        services.AddSingleton(cakeContext.Globber);
        services.AddSingleton(cakeContext.Arguments);
        services.AddSingleton(cakeContext.Configuration);

        // Host singletons (phase-x §6.5 BuildContext slim aggregate + per-axis
        // sub-records still individually injectable).
        services.AddSingleton<IPathService>(pathService);
        services.AddSingleton(runtimeProfile);
        services.AddSingleton(manifest);
        services.AddSingleton(options);
        services.AddSingleton(options.Vcpkg);
        services.AddSingleton(options.Package);
        services.AddSingleton(options.Versioning);
        services.AddSingleton(options.Repository);
        services.AddSingleton(options.DotNet);
        services.AddSingleton(options.Dumpbin);
        services.AddSingleton(new RuntimeConfig { Runtimes = manifest.Runtimes });
        services.AddSingleton(manifest.SystemExclusions);

        // Tools / Integrations — NSubstitute fakes for interfaces, concrete for the sealed
        // VcpkgBootstrapTool wrapper.
        services.AddSingleton(Substitute.For<IPackageInfoProvider>());
        services.AddSingleton(Substitute.For<ICoberturaReader>());
        services.AddSingleton(Substitute.For<ICoverageBaselineReader>());
        services.AddSingleton(Substitute.For<IVcpkgManifestReader>());
        services.AddSingleton(Substitute.For<IProjectMetadataReader>());
        services.AddSingleton(Substitute.For<IDotNetPackInvoker>());
        services.AddSingleton(Substitute.For<IDotNetRuntimeEnvironment>());
        services.AddSingleton(Substitute.For<INuGetFeedClient>());
        services.AddSingleton(Substitute.For<IMsvcDevEnvironment>());
        services.AddSingleton(Substitute.For<IRuntimeScanner>());
        services.AddSingleton<VcpkgBootstrapTool>();

        return services;
    }
}
