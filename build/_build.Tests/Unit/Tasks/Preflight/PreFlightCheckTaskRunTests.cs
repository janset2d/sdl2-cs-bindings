using Build.Tests.Fixtures;
using Build.Tasks.Preflight;
using IOPath = System.IO.Path;

namespace Build.Tests.Unit.Tasks.Preflight;

public class PreFlightCheckTaskRunTests : TempDirectoryTestBase
{
    [Test]
    public async Task Run_Should_Pass_When_Manifest_And_Vcpkg_Versions_Are_Aligned()
    {
        var repoRoot = CreateTempRepoRoot();

    await WriteManifestJsonAsync(repoRoot, "2.32.10", 0);
    await WriteVcpkgJsonAsync(repoRoot, "2.32.10", 0);

    var context = TaskTestHelpers.CreateBuildContextForRepoRoot(repoRoot);
    var task = new PreFlightCheckTask();

    task.Run(context);
    }

    [Test]
    public async Task Run_Should_Throw_When_Override_Version_Does_Not_Match_Manifest()
    {
        var repoRoot = CreateTempRepoRoot();

      await WriteManifestJsonAsync(repoRoot, "2.32.10", 0);
      await WriteVcpkgJsonAsync(repoRoot, "2.31.0", 0);

      var context = TaskTestHelpers.CreateBuildContextForRepoRoot(repoRoot);
      var task = new PreFlightCheckTask();

      await Assert.That(() => task.Run(context)).Throws<InvalidOperationException>();
    }

    [Test]
    public async Task Run_Should_Throw_When_Runtime_Strategy_And_Triplet_Are_Incoherent()
    {
        var repoRoot = CreateTempRepoRoot();

      await WriteManifestJsonAsync(
        repoRoot,
        "2.32.10",
        0,
        strategy: "pure-dynamic",
        triplet: "x64-windows-hybrid");
      await WriteVcpkgJsonAsync(repoRoot, "2.32.10", 0);

      var context = TaskTestHelpers.CreateBuildContextForRepoRoot(repoRoot);
      var task = new PreFlightCheckTask();

      await Assert.That(() => task.Run(context)).Throws<InvalidOperationException>();
    }

    [Test]
    public async Task Run_Should_Allow_Libraries_Without_Vcpkg_Overrides()
    {
        var repoRoot = CreateTempRepoRoot();

      await WriteManifestJsonAsync(repoRoot, "2.32.10", 0);
      await File.WriteAllTextAsync(IOPath.Combine(repoRoot, "vcpkg.json"), "{\"dependencies\":[\"sdl2\"]}");

      var context = TaskTestHelpers.CreateBuildContextForRepoRoot(repoRoot);
      var task = new PreFlightCheckTask();

      task.Run(context);
    }

    private static async Task WriteManifestJsonAsync(
        string repoRoot,
        string version,
        int portVersion,
        string strategy = "hybrid-static",
        string triplet = "x64-windows-hybrid")
    {
        var buildDir = IOPath.Combine(repoRoot, "build");
        Directory.CreateDirectory(buildDir);

        var manifestJson = $$"""
        {
          "schema_version": "2.1",
          "packaging_config": {
            "validation_mode": "strict",
            "core_library": "sdl2"
          },
          "runtimes": [
            {
              "rid": "win-x64",
              "triplet": "{{triplet}}",
              "strategy": "{{strategy}}",
              "runner": "windows-latest",
              "container_image": null
            }
          ],
          "package_families": [
            {
              "name": "core",
              "tag_prefix": "core",
              "managed_project": "src/SDL2.Core/SDL2.Core.csproj",
              "native_project": "src/native/SDL2.Core.Native/SDL2.Core.Native.csproj",
              "library_ref": "SDL2",
              "depends_on": [],
              "change_paths": [
                "src/SDL2.Core/**",
                "src/native/SDL2.Core.Native/**"
              ]
            }
          ],
          "system_exclusions": {
            "windows": {
              "system_dlls": ["kernel32.dll"]
            },
            "linux": {
              "system_libraries": ["libc.so*"]
            },
            "osx": {
              "system_libraries": ["libSystem.B.dylib"]
            }
          },
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

    private string CreateTempRepoRoot()
    {
      return CreateTrackedTempDirectory("preflight-task");
    }

}
