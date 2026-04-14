using System.Diagnostics;
using Build.Context;
using Build.Context.Configs;
using Build.Modules.Contracts;
using Build.Tasks.Preflight;
using Cake.Core;
using Cake.Core.Configuration;
using Cake.Core.IO;
using Cake.Core.Tooling;
using Cake.Testing;
using NSubstitute;
using IOPath = System.IO.Path;

namespace Build.Tests.Unit.Tasks.Preflight;

public class PreFlightCheckTaskRunTests
{
    [Test]
    public async Task Run_Should_Pass_When_Manifest_And_Vcpkg_Versions_Are_Aligned()
    {
        var repoRoot = CreateTempRepoRoot();
        try
        {
            await WriteManifestJsonAsync(repoRoot, "2.32.10", 0);
            await WriteVcpkgJsonAsync(repoRoot, "2.32.10", 0);

            var context = CreateBuildContext(repoRoot);
            var task = new PreFlightCheckTask();

            task.Run(context);
        }
        finally
        {
            DeleteDirectoryQuietly(repoRoot);
        }
    }

    [Test]
    public async Task Run_Should_Throw_When_Override_Version_Does_Not_Match_Manifest()
    {
        var repoRoot = CreateTempRepoRoot();
        try
        {
            await WriteManifestJsonAsync(repoRoot, "2.32.10", 0);
            await WriteVcpkgJsonAsync(repoRoot, "2.31.0", 0);

            var context = CreateBuildContext(repoRoot);
            var task = new PreFlightCheckTask();

            await Assert.That(() => task.Run(context)).Throws<InvalidOperationException>();
        }
        finally
        {
            DeleteDirectoryQuietly(repoRoot);
        }
    }

    [Test]
    public async Task Run_Should_Allow_Libraries_Without_Vcpkg_Overrides()
    {
        var repoRoot = CreateTempRepoRoot();
        try
        {
            await WriteManifestJsonAsync(repoRoot, "2.32.10", 0);
            await File.WriteAllTextAsync(IOPath.Combine(repoRoot, "vcpkg.json"), "{\"dependencies\":[\"sdl2\"]}");

            var context = CreateBuildContext(repoRoot);
            var task = new PreFlightCheckTask();

            task.Run(context);
        }
        finally
        {
            DeleteDirectoryQuietly(repoRoot);
        }
    }

    private static BuildContext CreateBuildContext(string repoRoot)
    {
        var environment = FakeEnvironment.CreateWindowsEnvironment();
        var fileSystem = new FileSystem();
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

        var pathService = Substitute.For<IPathService>();
        pathService.RepoRoot.Returns(new DirectoryPath(repoRoot));
        pathService.GetManifestFile().Returns(new FilePath(IOPath.Combine(repoRoot, "build", "manifest.json")));

        return new BuildContext(
            cakeContext,
            pathService,
            new RepositoryConfiguration(new DirectoryPath(repoRoot)),
            new DotNetBuildConfiguration("Release"),
            new VcpkgConfiguration([], null),
            new DumpbinConfiguration([]));
    }

    private static async Task WriteManifestJsonAsync(string repoRoot, string version, int portVersion)
    {
        var buildDir = IOPath.Combine(repoRoot, "build");
        Directory.CreateDirectory(buildDir);

        var manifestJson = $$"""
        {
          "schema_version": "2.0",
          "library_manifests": [
            {
              "name": "SDL2",
              "vcpkg_name": "sdl2",
              "vcpkg_version": "{{version}}",
              "vcpkg_port_version": {{portVersion}},
              "native_lib_name": "SDL2.Core.Native",
              "native_lib_version": "2.32.10.0",
              "core_lib": true,
              "primary_binaries": [
                { "os": "Windows", "patterns": ["SDL2.dll"] },
                { "os": "Linux", "patterns": ["libSDL2*"] },
                { "os": "OSX", "patterns": ["libSDL2*.dylib"] }
              ]
            }
          ]
        }
        """;

        await File.WriteAllTextAsync(IOPath.Combine(buildDir, "manifest.json"), manifestJson);
    }

    private static async Task WriteVcpkgJsonAsync(string repoRoot, string version, int portVersion)
    {
        var vcpkgJson = $$"""
        {
          "overrides": [
            {
              "name": "sdl2",
              "version": "{{version}}",
              "port-version": {{portVersion}}
            }
          ]
        }
        """;

        await File.WriteAllTextAsync(IOPath.Combine(repoRoot, "vcpkg.json"), vcpkgJson);
    }

    private static string CreateTempRepoRoot()
    {
        var path = IOPath.Combine(IOPath.GetTempPath(), "sdl2-bindings-tests", "preflight-task", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }

    private static void DeleteDirectoryQuietly(string path)
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
}
