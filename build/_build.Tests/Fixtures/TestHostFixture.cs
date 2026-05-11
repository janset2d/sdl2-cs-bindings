using Build.Host.Paths;
using Build.DependencyAnalysis;
using Build.Data.ProjectMetadata;
using Build.Targets.PackageConsumerSmoke.Services;
using Build.Targets.PublishStaging.Services;
using Build.Data.Manifest.Models;
using Build.Targets.NativeSmoke.Services;
using Build.Targets.Package.Services;
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
/// code path under <c>Program.cs ConfigureBuildServices</c> minus the per-target
/// <c>AddX()</c> calls. Tests then add a single target on top and assert
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
/// Tools and target-local services are NSubstitute-backed for interface registrations.
/// Vcpkg command execution is exercised through Cake tool aliases and fake process
/// results instead of DI-registered command providers. Scanners live in
/// <c>Build.DependencyAnalysis</c>, NuGet client in <c>Targets/PublishStaging/Services/</c>,
/// .NET runtime env in <c>Targets/PackageConsumerSmoke/Services/</c>, and project metadata
/// reader in <c>Build.Data.ProjectMetadata</c>.
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

        // Host singletons. Configurations aggregate + DumpbinConfiguration retired in S14;
        // VcpkgConfiguration retired in S15 (inlined into IRuntimeProfile factory).
        services.AddSingleton<IPathService>(pathService);
        services.AddSingleton(runtimeProfile);
        services.AddSingleton(manifest);
        services.AddSingleton(new RuntimeConfig { Runtimes = manifest.Runtimes });
        services.AddSingleton(manifest.SystemExclusions);

        // Tools / integrations that remain interface-backed in target collaborators.
        // Vcpkg command execution is exercised through Cake tool aliases and FakeCakeWorldV2
        // process results instead of a DI-registered provider.
        services.AddSingleton(Substitute.For<IProjectMetadataReader>());
        services.AddSingleton(Substitute.For<IDotNetPackInvoker>());
        services.AddSingleton(Substitute.For<IDotNetRuntimeEnvironment>());
        services.AddSingleton(Substitute.For<INuGetFeedClient>());
        services.AddSingleton(Substitute.For<IMsvcDevEnvironment>());
        services.AddSingleton(Substitute.For<IRuntimeScanner>());

        return services;
    }
}
