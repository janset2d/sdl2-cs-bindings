using Build.Data;
using Build.Targets.GenerateBindings;
using Build.Tests.Fixtures;
using Build.Validation;
using Cake.Core.IO;
using NSubstitute;
using TUnit.Assertions.Enums;

namespace Build.Tests.Scenarios.GenerateBindings;

public sealed class StampCheckTests
{
    private const string Generated = "spikes/binding-generators/clangsharp/src/Janset.SDL2.Image/Generated/";
    private const string Include = "vcpkg_installed/x64-windows-hybrid/include/SDL2/";
    private const string Remedy = "run tools.cs build --target GenerateBindings --execute --scope sdl2-image ... and commit the generated files and the stamp.";
    private static readonly string[] ConfigFields = ["config_fingerprint", "config_inputs", "generator_source_fingerprint"];

    [Test]
    public async Task RunAsync_Should_Report_Each_Missing_Output_When_Directory_Enumeration_Rejects_Absent_Roots()
    {
        var world = GeneratedStampWorld.Create();
        world.FileSystem.GetDirectory(world.RepoRoot.Combine("vcpkg-overlay-ports/sdl2-image")).Delete(true);
        await GeneratedStampWorld.SealAsync(world);
        world.FileSystem.GetDirectory(world.RepoRoot.Combine(Generated + "Compat")).Delete(true);
        world.FileSystem.GetDirectory(world.RepoRoot.Combine(Generated + "Modern")).Delete(true);
        var system = Substitute.For<IFileSystem>();
        system.GetFile(Arg.Any<FilePath>()).Returns(call => world.FileSystem.GetFile(call.Arg<FilePath>()));
        system.GetDirectory(Arg.Any<DirectoryPath>()).Returns(call =>
        {
            var path = call.Arg<DirectoryPath>();
            var directory = world.FileSystem.GetDirectory(path);

            if (directory.Exists)
            {
                return directory;
            }

            var absent = Substitute.For<IDirectory>();
            absent.Path.Returns(path);
            absent.Exists.Returns(false);
            absent.GetFiles(Arg.Any<string>(), Arg.Any<SearchScope>()).Returns(_ => throw new DirectoryNotFoundException(path.FullPath));
            return absent;
        });
        world.CakeContext.FileSystem.Returns(system);
        world.WithStampCheck();
        var result = await CreateHost(world).RunAsync();
        await Assert.That(result.Exception?.Message.Split('\n')).IsEquivalentTo([
            "sdl2-image: stale generated_file_count; " + Remedy,
            "sdl2-image: generated_files: missing Compat/a.g.cs; " + Remedy,
            "sdl2-image: generated_files: missing Modern/z.g.cs; " + Remedy,
        ], CollectionOrdering.Matching);
    }

    [Test]
    [Arguments("Directory.Packages.props", "5.9.0", "5.9.1")]
    [Arguments("dotnet-tools.json", "17.0.1", "17.0.2")]
    [Arguments("global.json", "11.0.100", "11.0.101")]
    public async Task RunAsync_Should_Check_Declared_Toolchain_Identities_Without_Launching_Tools(string path, string oldValue, string newValue)
    {
        var world = GeneratedStampWorld.Create();
        await GeneratedStampWorld.SealAsync(world);
        world.WithTextFile(path, world.ReadAllText(path).Replace(oldValue, newValue, StringComparison.Ordinal)).WithStampCheck();
        var before = Snapshot(world);
        var result = await CreateHost(world).RunAsync();
        await Assert.That(result.Exception?.Message.Split('\n')).IsEquivalentTo([
            "sdl2-image: stale generator_source_fingerprint; " + Remedy,
            "sdl2-image: stale toolchain; " + Remedy,
        ], CollectionOrdering.Matching);
        await AssertReadOnlyAsync(world, before);
    }

    [Test]
    [Arguments("build/binding-generation/rsp/sdl2-image.rsp", "stale generator_source_fingerprint,stale rsp_inputs")]
    [Arguments("build/_build/Host/Cake/CakeFileSystemExtensions.cs", "stale generator_inputs,stale generator_source_fingerprint")]
    [Arguments(Generated + "Compat/a.g.cs", "generated_files: edited Compat/a.g.cs")]
    public async Task RunAsync_Should_Keep_Checking_Nonheader_Changes_When_Headers_Are_Absent(string path, string fields)
    {
        ArgumentNullException.ThrowIfNull(fields);
        var world = GeneratedStampWorld.Create();
        await GeneratedStampWorld.SealAsync(world);
        world.FileSystem.GetDirectory(world.RepoRoot.Combine("vcpkg_installed/x64-windows-hybrid")).Delete(true);
        world.WithTextFile(path, world.ReadAllText(path) + " ").WithStampCheck();
        var before = Snapshot(world);
        var result = await CreateHost(world).RunAsync();
        await Assert.That(result.Exception?.Message.Split('\n'))
            .IsEquivalentTo(fields.Split(',').Select(field => "sdl2-image: " + field + "; " + Remedy), CollectionOrdering.Matching);
        await AssertReadOnlyAsync(world, before);
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task RunAsync_Should_Check_Fresh_Recorded_Windows_Inputs_On_A_Different_Host_Without_Writes(bool requireHeaders)
    {
        var world = GeneratedStampWorld.Create();
        await GeneratedStampWorld.SealAsync(world);
        world.WithStampCheck(requireHeaders).WithGeneration(false, platformHeaderShims: false);
        RefuseFileWrites(world);
        var before = Snapshot(world);
        var result = await CreateHost(world).RunAsync();
        await Assert.That(result.Exception).IsNull();
        await Assert.That(world.Log.Entries.Where(entry => entry.Message.StartsWith("sdl2-image:", StringComparison.Ordinal))).IsEmpty();
        await AssertReadOnlyAsync(world, before);
    }

    [Test]
    [Arguments("build/_build/Targets/GenerateBindings/PostProcess/sample.cs", "generator_inputs,generator_source_fingerprint")]
    [Arguments("build/_build/Tools/ClangSharp/sample.cs", "generator_inputs,generator_source_fingerprint")]
    [Arguments("build/_build/Data/BindingGeneration/sample.cs", "generator_inputs,generator_source_fingerprint")]
    [Arguments("build/_build/Data/Manifest/sample.cs", "generator_inputs,generator_source_fingerprint")]
    [Arguments("build/_build/Host/Cake/CakeFileSystemExtensions.cs", "generator_inputs,generator_source_fingerprint")]
    [Arguments("build/_build/Build.csproj", "generator_inputs,generator_source_fingerprint")]
    [Arguments("build/_build/packages.lock.json", "generator_inputs,generator_source_fingerprint")]
    [Arguments("build/binding-generation/rsp/sdl2-image.rsp", "generator_source_fingerprint,rsp_inputs")]
    [Arguments("build/binding-generation/shims/platform-headers/a.h", "generator_source_fingerprint,header_fingerprint,header_inputs,shim_inputs")]
    [Arguments("build/binding-generation/shims/platform-headers/nested/b.h", "generator_source_fingerprint,header_fingerprint,header_inputs,shim_inputs")]
    [Arguments("vcpkg-overlay-ports/sdl2-image/patch.diff", "vcpkg_inputs")]
    [Arguments("vcpkg-overlay-triplets/x64-windows-hybrid.cmake", "vcpkg_inputs")]
    [Arguments("vcpkg.json", "vcpkg_inputs,vcpkg_manifest_hash")]
    [Arguments(Include + "SDL.h", "header_fingerprint,header_inputs")]
    [Arguments(Include + "nested/transitive.h", "header_fingerprint,header_inputs")]
    public async Task RunAsync_Should_Report_The_Complete_Ordered_Stale_Field_List_For_One_Byte_Input_Drift(string path, string fields)
    {
        ArgumentNullException.ThrowIfNull(fields);
        var world = GeneratedStampWorld.Create();
        await GeneratedStampWorld.SealAsync(world);
        world.WithTextFile(path, world.ReadAllText(path) + " ").WithStampCheck(requireHeaders: true);
        var before = Snapshot(world);
        var result = await CreateHost(world).RunAsync();
        var expected = fields.Split(',').Select(field => "sdl2-image: stale " + field + "; " + Remedy);
        await Assert.That(result.Exception?.Message.Split('\n')).IsEquivalentTo(expected, CollectionOrdering.Matching);
        await AssertReadOnlyAsync(world, before);
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task RunAsync_Should_Report_Header_Unavailability_And_Require_It_Only_When_Requested(bool requireHeaders)
    {
        var world = GeneratedStampWorld.Create();
        await GeneratedStampWorld.SealAsync(world);
        world.FileSystem.GetDirectory(world.RepoRoot.Combine("vcpkg_installed/x64-windows-hybrid")).Delete(true);
        world.WithTextFile("vcpkg_installed/x64-linux-hybrid/include/SDL2/SDL_image.h", "wrong triplet must not be compared").WithStampCheck(requireHeaders);
        RefuseFileWrites(world);
        var before = Snapshot(world);
        var result = await CreateHost(world).RunAsync();
        var message = "sdl2-image: header content not checked (header_inputs, header_fingerprint, header_input_count); recorded triplet x64-windows-hybrid is not installed.";
        await Assert.That(world.Log.Entries.Where(entry => entry.Message.StartsWith("sdl2-image:", StringComparison.Ordinal)).Select(entry => entry.Message))
            .IsEquivalentTo([requireHeaders ? message + " " + Remedy : message], CollectionOrdering.Matching);
        await Assert.That(result.Exception?.Message).IsEqualTo(requireHeaders ? message + " " + Remedy : null);
        await AssertReadOnlyAsync(world, before);
    }

    [Test]
    [Arguments("edit", "generated_files: edited Compat/a.g.cs")]
    [Arguments("remove", "stale generated_file_count,generated_files: missing Compat/a.g.cs")]
    [Arguments("extra", "stale generated_file_count,generated_files: extra extra.g.cs")]
    public async Task RunAsync_Should_Distinguish_Output_Edit_Deletion_And_Extra_File(string mutation, string fields)
    {
        ArgumentNullException.ThrowIfNull(fields);
        var world = GeneratedStampWorld.Create();
        await GeneratedStampWorld.SealAsync(world);

        switch (mutation)
        {
            case "edit": world.WithTextFile(Generated + "Compat/a.g.cs", "edited\n"); break;
            case "remove": world.FileSystem.GetFile(world.RepoRoot.CombineWithFilePath(Generated + "Compat/a.g.cs")).Delete(); break;
            case "extra": world.WithTextFile(Generated + "extra.g.cs", "extra\n"); break;
        }

        world.WithStampCheck();
        var before = Snapshot(world);
        var result = await CreateHost(world).RunAsync();
        await Assert.That(result.Exception?.Message.Split('\n'))
            .IsEquivalentTo(fields.Split(',').Select(field => "sdl2-image: " + field + "; " + Remedy), CollectionOrdering.Matching);
        await AssertReadOnlyAsync(world, before);
    }

    [Test]
    [Arguments("selected")]
    [Arguments("transitive")]
    [Arguments("added")]
    public async Task RunAsync_Should_Refuse_Missing_Selected_And_Changed_Header_Counts(string mutation)
    {
        var world = GeneratedStampWorld.Create();
        await GeneratedStampWorld.SealAsync(world);
        string[] fields;

        if (mutation == "selected")
        {
            world.FileSystem.GetFile(world.RepoRoot.CombineWithFilePath(Include + "SDL_image.h")).Delete();
            fields = ["header_inputs: missing selected header: SDL_image.h"];
        }
        else
        {
            if (mutation == "transitive")
            {
                world.FileSystem.GetFile(world.RepoRoot.CombineWithFilePath(Include + "nested/transitive.h")).Delete();
            }
            else
            {
                world.WithTextFile(Include + "extra.h", "extra\n");
            }

            fields = ["stale header_fingerprint", "stale header_input_count", "stale header_inputs"];
        }

        world.WithStampCheck(requireHeaders: true);
        var before = Snapshot(world);
        var result = await CreateHost(world).RunAsync();
        await Assert.That(result.Exception?.Message.Split('\n'))
            .IsEquivalentTo(fields.Select(field => "sdl2-image: " + field + "; " + Remedy), CollectionOrdering.Matching);
        await AssertReadOnlyAsync(world, before);
    }

    [Test]
    public async Task RunAsync_Should_Invalidate_A_Selected_Satellite_When_Core_Handles_Change()
    {
        var world = GeneratedStampWorld.Create();
        await GeneratedStampWorld.SealAsync(world);
        world.WithTextFile("build/manifest.json", world.ReadAllText("build/manifest.json").Replace("\"SDL_Window\"", "\"SDL_Windox\"", StringComparison.Ordinal));
        world.WithStampCheck();
        var result = await CreateHost(world).RunAsync();
        await Assert.That(result.Exception?.Message.Split('\n')).IsEquivalentTo(
            ConfigFields.Select(field => "sdl2-image: stale " + field + "; " + Remedy),
            CollectionOrdering.Matching);
    }

    [Test]
    [Arguments("99", "unknown schema_version: 99")]
    [Arguments("null", "invalid stamp schema_version")]
    public async Task RunAsync_Should_Refuse_An_Unknown_Or_Null_Schema(string schema, string diagnostic)
    {
        var world = GeneratedStampWorld.Create();
        await GeneratedStampWorld.SealAsync(world);
        world.WithTextFile(Generated + ".generated-stamp", world.ReadAllText(Generated + ".generated-stamp").Replace("\"schema_version\": 2", "\"schema_version\": " + schema,
            StringComparison.Ordinal));
        world.WithStampCheck();
        var result = await CreateHost(world).RunAsync();
        await Assert.That(result.Exception?.Message).IsEqualTo("sdl2-image: " + diagnostic + "; " + Remedy);
    }

    [Test]
    [Arguments(true, false)]
    [Arguments(false, true)]
    [Arguments(true, true)]
    public async Task RunAsync_Should_Reject_Check_With_Execute_Or_Evidence_Before_Work(bool execute, bool evidence)
    {
        var world = GeneratedStampWorld.Create().WithStampCheck().WithGeneration(execute).WithEvidence(evidence);
        var before = Snapshot(world);
        var result = await CreateHost(world).RunAsync();
        await Assert.That(result.Exception?.Message).IsEqualTo("--check cannot be combined with --execute or --evidence.");
        await AssertReadOnlyAsync(world, before);
    }

    [Test]
    public async Task RunAsync_Should_Report_Config_Drift_In_Headerless_Mode()
    {
        var world = GeneratedStampWorld.Create();
        await GeneratedStampWorld.SealAsync(world);
        world.WithTextFile("build/manifest.json", world.ReadAllText("build/manifest.json").Replace("\"SDL2.Image\"", "\"SDL2.Imagx\"", StringComparison.Ordinal));
        world.FileSystem.GetDirectory(world.RepoRoot.Combine("vcpkg_installed/x64-windows-hybrid")).Delete(true);
        world.WithStampCheck();
        var result = await CreateHost(world).RunAsync();
        await Assert.That(result.Exception?.Message.Split('\n'))
            .IsEquivalentTo(ConfigFields.Select(field => "sdl2-image: stale " + field + "; " + Remedy), CollectionOrdering.Matching);
    }

    [Test]
    public async Task RunAsync_Should_Refuse_A_Missing_Stamp_Without_Process_Or_Write()
    {
        var world = FakeCakeWorld.CreateLinux().WithManifestObject(ManifestFixture.RealManifest).WithStampCheck().WithScope("sdl2-image");
        var before = Snapshot(world);
        var result = await CreateHost(world).RunAsync();
        await Assert.That(result.Exception?.Message).IsEqualTo(
            "sdl2-image: missing stamp; run tools.cs build --target GenerateBindings --execute --scope sdl2-image ... and commit the generated files and the stamp.");
        await AssertReadOnlyAsync(world, before);
    }

    internal static TargetTestHost<GenerateBindingsTask> CreateHost(FakeCakeWorld world)
        => new TargetTestHost<GenerateBindingsTask>(world).WithServices(services => services.AddData().AddValidators());

    private static void RefuseFileWrites(FakeCakeWorld world)
    {
        var system = Substitute.For<IFileSystem>();
        system.GetDirectory(Arg.Any<DirectoryPath>()).Returns(call => world.FileSystem.GetDirectory(call.Arg<DirectoryPath>()));
        system.GetFile(Arg.Any<FilePath>()).Returns(call =>
        {
            var path = call.Arg<FilePath>();
            var original = world.FileSystem.GetFile(path);
            var file = Substitute.For<IFile>();
            file.Path.Returns(path);
            file.Exists.Returns(original.Exists);
            file.Open(Arg.Any<FileMode>(), Arg.Any<FileAccess>(), Arg.Any<FileShare>()).Returns(invocation =>
            {
                if (invocation.Arg<FileAccess>() != FileAccess.Read)
                {
                    throw new IOException("The stamp check attempted a file write.");
                }

                return original.Open(invocation.Arg<FileMode>(), FileAccess.Read, invocation.Arg<FileShare>());
            });
            return file;
        });
        world.CakeContext.FileSystem.Returns(system);
    }

    internal static string[] Snapshot(FakeCakeWorld world) => [.. world.FileSystem
        .GetDirectory(world.RepoRoot)
        .GetFiles("*", SearchScope.Recursive)
        .OrderBy(file => file.Path.FullPath, StringComparer.Ordinal)
        .Select(file => file.Path.FullPath + "|" + world.ReadAllText(world.RepoRoot.GetRelativePath(file.Path).FullPath))];

    internal static async Task AssertReadOnlyAsync(FakeCakeWorld world, string[] before)
    {
        await Assert.That(Snapshot(world)).IsEquivalentTo(before, CollectionOrdering.Matching);
        await Assert.That(world.ProcessInvocations).IsEmpty();
        await Assert.That(world.HostProcessInvocations).IsEmpty();
    }
}
