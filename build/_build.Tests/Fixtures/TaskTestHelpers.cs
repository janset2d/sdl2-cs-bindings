using System.Diagnostics;
using Build.Context;
using Build.Context.Configs;
using Build.Modules.Contracts;
using Cake.Core;
using Cake.Core.Configuration;
using Cake.Core.IO;
using Cake.Core.Tooling;
using Cake.Testing;
using NSubstitute;
using IOPath = System.IO.Path;

namespace Build.Tests.Fixtures;

public static class TaskTestHelpers
{
    public static BuildContext CreateBuildContext(DirectoryPath harvestOutput, IReadOnlyList<string>? libraries = null)
    {
        var environment = FakeEnvironment.CreateWindowsEnvironment();
        var fileSystem = new FileSystem();
        var cakeContext = CreateCakeContext(environment, fileSystem);

        var pathService = Substitute.For<IPathService>();
        pathService.HarvestOutput.Returns(harvestOutput);

        return new BuildContext(
            cakeContext,
            pathService,
            new RepositoryConfiguration(new DirectoryPath(IOPath.GetPathRoot(IOPath.GetTempPath()) ?? "C:/")),
            new DotNetBuildConfiguration("Release"),
            new VcpkgConfiguration(libraries ?? [], rid: null),
            new DumpbinConfiguration([]));
    }

    public static BuildContext CreateBuildContextForRepoRoot(string repoRoot, IReadOnlyList<string>? libraries = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(repoRoot);

        var environment = FakeEnvironment.CreateWindowsEnvironment();
        var fileSystem = new FileSystem();
        var cakeContext = CreateCakeContext(environment, fileSystem);
        var repoRootPath = new DirectoryPath(repoRoot);

        var pathService = Substitute.For<IPathService>();
        pathService.RepoRoot.Returns(repoRootPath);
        pathService.GetManifestFile().Returns(repoRootPath.Combine("build").CombineWithFilePath("manifest.json"));
        pathService.GetCoverageBaselineFile().Returns(repoRootPath.Combine("build").CombineWithFilePath("coverage-baseline.json"));

        return new BuildContext(
            cakeContext,
            pathService,
            new RepositoryConfiguration(repoRootPath),
            new DotNetBuildConfiguration("Release"),
            new VcpkgConfiguration(libraries ?? [], rid: null),
            new DumpbinConfiguration([]));
    }

    public static void DeleteDirectoryQuietly(string path)
    {
        try
        {
            if (Directory.Exists(path))
            {
                Directory.Delete(path, recursive: true);
            }
        }
        catch (IOException)
        {
            Debug.WriteLine($"Unable to delete test directory: {path}");
        }
        catch (UnauthorizedAccessException)
        {
            Debug.WriteLine($"Unauthorized while deleting test directory: {path}");
        }
    }

    private static ICakeContext CreateCakeContext(ICakeEnvironment environment, IFileSystem fileSystem)
    {
        var globber = new Globber(fileSystem, environment);

        var cakeContext = Substitute.For<ICakeContext>();
        cakeContext.Log.Returns(new FakeLog());
        cakeContext.Environment.Returns(environment);
        cakeContext.FileSystem.Returns(fileSystem);
        cakeContext.Globber.Returns(globber);
        cakeContext.Arguments.Returns(Substitute.For<ICakeArguments>());
        cakeContext.Configuration.Returns(Substitute.For<ICakeConfiguration>());
        cakeContext.Data.Returns(Substitute.For<ICakeDataResolver>());
        cakeContext.ProcessRunner.Returns(Substitute.For<IProcessRunner>());
        cakeContext.Registry.Returns(Substitute.For<IRegistry>());
        cakeContext.Tools.Returns(Substitute.For<IToolLocator>());

        return cakeContext;
    }
}
