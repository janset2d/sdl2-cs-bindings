using Build.Tests.Fixtures;
using Build.Tasks.Preflight;
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

            var context = TaskTestHelpers.CreateBuildContextForRepoRoot(repoRoot);
            var task = new PreFlightCheckTask();

            task.Run(context);
        }
        finally
        {
            TaskTestHelpers.DeleteDirectoryQuietly(repoRoot);
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

            var context = TaskTestHelpers.CreateBuildContextForRepoRoot(repoRoot);
            var task = new PreFlightCheckTask();

            await Assert.That(() => task.Run(context)).Throws<InvalidOperationException>();
        }
        finally
        {
            TaskTestHelpers.DeleteDirectoryQuietly(repoRoot);
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

            var context = TaskTestHelpers.CreateBuildContextForRepoRoot(repoRoot);
            var task = new PreFlightCheckTask();

            task.Run(context);
        }
        finally
        {
            TaskTestHelpers.DeleteDirectoryQuietly(repoRoot);
        }
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

}
