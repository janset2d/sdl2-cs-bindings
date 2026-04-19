using System.Collections.Immutable;
using Build.Context.Models;
using Build.Domain.Preflight;
using Build.Domain.Preflight.Models;
using Build.Domain.Preflight.Results;
using Build.Tests.Fixtures;
using Cake.Core.IO;

namespace Build.Tests.Unit.Domain.Preflight;

/// <summary>
/// Post-S1 scope: G1/G2/G3/G5/G8 retired. Validator enforces G4, G6, G7, G17, G18 only.
/// </summary>
public sealed class CsprojPackContractValidatorTests
{
    [Test]
    public async Task Validate_Should_Return_Success_When_All_Csprojs_Conform()
    {
        var (validator, repoRoot, manifest) = ArrangeWithFamily(
            ManagedCsproj("Janset.SDL2.Image", "sdl2-image-"),
            NativeCsproj("Janset.SDL2.Image.Native", "sdl2-image-"));

        var result = validator.Validate(manifest, repoRoot);

        await Assert.That(result.IsSuccess()).IsTrue();
        await Assert.That(result.Validation.HasErrors).IsFalse();
    }

    [Test]
    public async Task Validate_Should_Fail_When_MinVerTagPrefix_Drifts_From_Manifest()
    {
        const string managedCsproj = """
                                     <Project Sdk="Microsoft.NET.Sdk">
                                       <PropertyGroup>
                                         <PackageId>Janset.SDL2.Image</PackageId>
                                         <MinVerTagPrefix>image-</MinVerTagPrefix>
                                       </PropertyGroup>
                                       <ItemGroup>
                                         <ProjectReference Include="..\native\SDL2.Image.Native\SDL2.Image.Native.csproj" />
                                       </ItemGroup>
                                     </Project>
                                     """;
        var (validator, repoRoot, manifest) = ArrangeWithFamily(
            (Path: "src/SDL2.Image/SDL2.Image.csproj", Content: managedCsproj),
            NativeCsproj("Janset.SDL2.Image.Native", "sdl2-image-"));

        var result = validator.Validate(manifest, repoRoot);

        await AssertFails(result, CsprojPackContractCheckKind.MinVerTagPrefixMatchesManifest);
    }

    [Test]
    public async Task Validate_Should_Fail_When_Managed_PackageId_Drifts_From_Canonical_Convention()
    {
        const string managedCsproj = """
                                     <Project Sdk="Microsoft.NET.Sdk">
                                       <PropertyGroup>
                                         <PackageId>Janset.SDL2.WrongName</PackageId>
                                         <MinVerTagPrefix>sdl2-image-</MinVerTagPrefix>
                                       </PropertyGroup>
                                       <ItemGroup>
                                         <ProjectReference Include="..\native\SDL2.Image.Native\SDL2.Image.Native.csproj" />
                                       </ItemGroup>
                                     </Project>
                                     """;
        var (validator, repoRoot, manifest) = ArrangeWithFamily(
            (Path: "src/SDL2.Image/SDL2.Image.csproj", Content: managedCsproj),
            NativeCsproj("Janset.SDL2.Image.Native", "sdl2-image-"));

        var result = validator.Validate(manifest, repoRoot);

        await AssertFails(result, CsprojPackContractCheckKind.PackageIdMatchesCanonicalConvention);
    }

    [Test]
    public async Task Validate_Should_Fail_When_Native_ProjectReference_Path_Does_Not_Match_Manifest()
    {
        const string managedCsproj = """
                                     <Project Sdk="Microsoft.NET.Sdk">
                                       <PropertyGroup>
                                         <PackageId>Janset.SDL2.Image</PackageId>
                                         <MinVerTagPrefix>sdl2-image-</MinVerTagPrefix>
                                       </PropertyGroup>
                                       <ItemGroup>
                                         <ProjectReference Include="..\native\SDL2.Unrelated.Native\SDL2.Unrelated.Native.csproj" />
                                       </ItemGroup>
                                     </Project>
                                     """;
        var (validator, repoRoot, manifest) = ArrangeWithFamily(
            (Path: "src/SDL2.Image/SDL2.Image.csproj", Content: managedCsproj),
            NativeCsproj("Janset.SDL2.Image.Native", "sdl2-image-"));

        var result = validator.Validate(manifest, repoRoot);

        await AssertFails(result, CsprojPackContractCheckKind.NativeProjectReferencePathMatchesManifest);
    }

    [Test]
    public async Task Validate_Should_Fail_When_Native_PackageId_Drifts_From_Canonical_Convention()
    {
        const string nativeCsproj = """
                                    <Project Sdk="Microsoft.NET.Sdk">
                                      <PropertyGroup>
                                        <PackageId>Janset.SDL2.Image.WrongNative</PackageId>
                                        <MinVerTagPrefix>sdl2-image-</MinVerTagPrefix>
                                      </PropertyGroup>
                                    </Project>
                                    """;
        var (validator, repoRoot, manifest) = ArrangeWithFamily(
            ManagedCsproj("Janset.SDL2.Image", "sdl2-image-"),
            (Path: "src/native/SDL2.Image.Native/SDL2.Image.Native.csproj", Content: nativeCsproj));

        var result = validator.Validate(manifest, repoRoot);

        await AssertFails(result, CsprojPackContractCheckKind.PackageIdMatchesCanonicalConvention);
    }

    [Test]
    public async Task Validate_Should_Fail_When_Csproj_Missing_From_Disk()
    {
        var family = new PackageFamilyConfig
        {
            Name = "sdl2-image",
            TagPrefix = "sdl2-image",
            ManagedProject = "src/SDL2.Image/SDL2.Image.csproj",
            NativeProject = "src/native/SDL2.Image.Native/SDL2.Image.Native.csproj",
            LibraryRef = "SDL2_image",
            DependsOn = ImmutableList<string>.Empty,
            ChangePaths = ImmutableList<string>.Empty,
        };
        var (validator, repoRoot, manifest) = Arrange([family], _ => { });

        var result = validator.Validate(manifest, repoRoot);

        await AssertFails(result, CsprojPackContractCheckKind.CsprojFileExists);
    }

    [Test]
    public async Task Validate_Should_Fail_When_DependsOn_References_Unknown_Family()
    {
        var family = new PackageFamilyConfig
        {
            Name = "sdl2-image",
            TagPrefix = "sdl2-image",
            ManagedProject = null,
            NativeProject = null,
            LibraryRef = "SDL2_image",
            DependsOn = ["sdl2-mythical-typo"],
            ChangePaths = ImmutableList<string>.Empty,
        };
        var (validator, repoRoot, manifest) = Arrange([family], _ => { });

        var result = validator.Validate(manifest, repoRoot);

        await AssertFails(result, CsprojPackContractCheckKind.DependsOnReferencesExistingFamily);
    }

    [Test]
    public async Task Validate_Should_Fail_When_LibraryRef_References_Unknown_Library()
    {
        var family = new PackageFamilyConfig
        {
            Name = "sdl2-image",
            TagPrefix = "sdl2-image",
            ManagedProject = null,
            NativeProject = null,
            LibraryRef = "SDL2_unknown_lib",
            DependsOn = ImmutableList<string>.Empty,
            ChangePaths = ImmutableList<string>.Empty,
        };
        var (validator, repoRoot, manifest) = Arrange([family], _ => { });

        var result = validator.Validate(manifest, repoRoot);

        await AssertFails(result, CsprojPackContractCheckKind.LibraryRefReferencesExistingLibrary);
    }

    private static (CsprojPackContractValidator Validator, DirectoryPath RepoRoot, ManifestConfig Manifest) ArrangeWithFamily(
        (string Path, string Content) managed,
        (string Path, string Content) native)
    {
        var family = new PackageFamilyConfig
        {
            Name = "sdl2-image",
            TagPrefix = "sdl2-image",
            ManagedProject = managed.Path,
            NativeProject = native.Path,
            LibraryRef = "SDL2_image",
            DependsOn = ImmutableList<string>.Empty,
            ChangePaths = ImmutableList<string>.Empty,
        };

        return Arrange([family], builder =>
        {
            builder.WithTextFile(managed.Path, managed.Content);
            builder.WithTextFile(native.Path, native.Content);
        });
    }

    private static (CsprojPackContractValidator Validator, DirectoryPath RepoRoot, ManifestConfig Manifest) Arrange(
        IReadOnlyList<PackageFamilyConfig> families,
        Action<FakeRepoBuilder> seedFiles)
    {
        var builder = new FakeRepoBuilder();
        seedFiles(builder);
        var handles = builder.BuildContextWithHandles();

        var manifest = ManifestFixture.CreateTestManifestConfig() with
        {
            PackageFamilies = [.. families],
        };
        var validator = new CsprojPackContractValidator(handles.FileSystem);
        return (validator, handles.RepoRoot, manifest);
    }

    private static (string Path, string Content) ManagedCsproj(string packageId, string tagPrefix)
    {
        var content = $"""
                       <Project Sdk="Microsoft.NET.Sdk">
                         <PropertyGroup>
                           <PackageId>{packageId}</PackageId>
                           <MinVerTagPrefix>{tagPrefix}</MinVerTagPrefix>
                         </PropertyGroup>
                         <ItemGroup>
                           <ProjectReference Include="..\native\SDL2.Image.Native\SDL2.Image.Native.csproj" />
                         </ItemGroup>
                       </Project>
                       """;

        return (Path: "src/SDL2.Image/SDL2.Image.csproj", Content: content);
    }

    private static (string Path, string Content) NativeCsproj(string packageId, string tagPrefix)
    {
        var content = $"""
                       <Project Sdk="Microsoft.NET.Sdk">
                         <PropertyGroup>
                           <PackageId>{packageId}</PackageId>
                           <MinVerTagPrefix>{tagPrefix}</MinVerTagPrefix>
                         </PropertyGroup>
                       </Project>
                       """;
        return (Path: "src/native/SDL2.Image.Native/SDL2.Image.Native.csproj", Content: content);
    }

    private static async Task AssertFails(CsprojPackContractResult result, CsprojPackContractCheckKind expectedKind)
    {
        await Assert.That(result.IsError()).IsTrue();
        await Assert.That(result.Validation.HasErrors).IsTrue();
        var hasExpectedKind = result.Validation.Checks.Any(check => !check.IsValid && check.Kind == expectedKind);
        await Assert.That(hasExpectedKind).IsTrue();
    }
}
