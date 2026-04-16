using System.Collections.Immutable;
using System.Linq;
using Build.Context.Models;
using Build.Modules.Preflight;
using Build.Modules.Preflight.Models;
using Build.Modules.Preflight.Results;
using Build.Tests.Fixtures;
using Cake.Core.IO;
using Cake.Testing;

namespace Build.Tests.Unit.Modules.Preflight;

public sealed class CsprojPackContractValidatorTests
{
    [Test]
    public async Task Validate_Should_Return_Success_When_All_Csprojs_Conform()
    {
        var (validator, repoRoot, manifest) = ArrangeWithFamily(
            ManagedCsproj("sdl2-image", "Janset.SDL2.Image", "sdl2-image-", FamilyVersionShape.Canonical, includeNativeRef: true),
            NativeCsproj("Janset.SDL2.Image.Native", "sdl2-image-"));

        var result = validator.Validate(manifest, repoRoot);

        await Assert.That(result.IsSuccess()).IsTrue();
        await Assert.That(result.Validation.HasErrors).IsFalse();
    }

    [Test]
    public async Task Validate_Should_Fail_When_Native_ProjectReference_Missing_PrivateAssets_All()
    {
        var managedCsproj = """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <PackageId>Janset.SDL2.Image</PackageId>
                <MinVerTagPrefix>sdl2-image-</MinVerTagPrefix>
                <Sdl2ImageFamilyVersion Condition="'$(Version)' != '' and '$(Sdl2ImageFamilyVersion)' == ''">$(Version)</Sdl2ImageFamilyVersion>
                <Sdl2ImageFamilyVersion Condition="'$(Sdl2ImageFamilyVersion)' == ''">0.0.0-restore</Sdl2ImageFamilyVersion>
              </PropertyGroup>
              <ItemGroup>
                <ProjectReference Include="..\native\SDL2.Image.Native\SDL2.Image.Native.csproj" />
              </ItemGroup>
              <ItemGroup>
                <PackageVersion Include="Janset.SDL2.Image.Native" Version="[$(Sdl2ImageFamilyVersion)]" />
                <PackageReference Include="Janset.SDL2.Image.Native" />
              </ItemGroup>
            </Project>
            """;
        var (validator, repoRoot, manifest) = ArrangeWithFamily(
            (Path: "src/SDL2.Image/SDL2.Image.csproj", Content: managedCsproj),
            NativeCsproj("Janset.SDL2.Image.Native", "sdl2-image-"));

        var result = validator.Validate(manifest, repoRoot);

        await AssertFails(result, CsprojPackContractCheckKind.NativeProjectReferenceHasPrivateAssetsAll);
    }

    [Test]
    public async Task Validate_Should_Fail_When_PackageVersion_Missing_Bracket_Notation()
    {
        var managedCsproj = """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <PackageId>Janset.SDL2.Image</PackageId>
                <MinVerTagPrefix>sdl2-image-</MinVerTagPrefix>
                <Sdl2ImageFamilyVersion Condition="'$(Version)' != '' and '$(Sdl2ImageFamilyVersion)' == ''">$(Version)</Sdl2ImageFamilyVersion>
                <Sdl2ImageFamilyVersion Condition="'$(Sdl2ImageFamilyVersion)' == ''">0.0.0-restore</Sdl2ImageFamilyVersion>
              </PropertyGroup>
              <ItemGroup>
                <ProjectReference Include="..\native\SDL2.Image.Native\SDL2.Image.Native.csproj" PrivateAssets="all" />
              </ItemGroup>
              <ItemGroup>
                <PackageVersion Include="Janset.SDL2.Image.Native" Version="$(Sdl2ImageFamilyVersion)" />
                <PackageReference Include="Janset.SDL2.Image.Native" />
              </ItemGroup>
            </Project>
            """;
        var (validator, repoRoot, manifest) = ArrangeWithFamily(
            (Path: "src/SDL2.Image/SDL2.Image.csproj", Content: managedCsproj),
            NativeCsproj("Janset.SDL2.Image.Native", "sdl2-image-"));

        var result = validator.Validate(manifest, repoRoot);

        await AssertFails(result, CsprojPackContractCheckKind.NativePackageVersionUsesBracketNotation);
    }

    [Test]
    public async Task Validate_Should_Fail_When_MinVerTagPrefix_Drifts_From_Manifest()
    {
        var managedCsproj = """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <PackageId>Janset.SDL2.Image</PackageId>
                <MinVerTagPrefix>image-</MinVerTagPrefix>
                <Sdl2ImageFamilyVersion Condition="'$(Version)' != '' and '$(Sdl2ImageFamilyVersion)' == ''">$(Version)</Sdl2ImageFamilyVersion>
                <Sdl2ImageFamilyVersion Condition="'$(Sdl2ImageFamilyVersion)' == ''">0.0.0-restore</Sdl2ImageFamilyVersion>
              </PropertyGroup>
              <ItemGroup>
                <ProjectReference Include="..\native\SDL2.Image.Native\SDL2.Image.Native.csproj" PrivateAssets="all" />
              </ItemGroup>
              <ItemGroup>
                <PackageVersion Include="Janset.SDL2.Image.Native" Version="[$(Sdl2ImageFamilyVersion)]" />
                <PackageReference Include="Janset.SDL2.Image.Native" />
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
    public async Task Validate_Should_Fail_When_PackageId_Drifts_From_Canonical_Convention()
    {
        var managedCsproj = """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <PackageId>Janset.SDL2.WrongName</PackageId>
                <MinVerTagPrefix>sdl2-image-</MinVerTagPrefix>
                <Sdl2ImageFamilyVersion Condition="'$(Version)' != '' and '$(Sdl2ImageFamilyVersion)' == ''">$(Version)</Sdl2ImageFamilyVersion>
                <Sdl2ImageFamilyVersion Condition="'$(Sdl2ImageFamilyVersion)' == ''">0.0.0-restore</Sdl2ImageFamilyVersion>
              </PropertyGroup>
              <ItemGroup>
                <ProjectReference Include="..\native\SDL2.Image.Native\SDL2.Image.Native.csproj" PrivateAssets="all" />
              </ItemGroup>
              <ItemGroup>
                <PackageVersion Include="Janset.SDL2.Image.Native" Version="[$(Sdl2ImageFamilyVersion)]" />
                <PackageReference Include="Janset.SDL2.Image.Native" />
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
    public async Task Validate_Should_Fail_When_Family_Version_Property_Missing_Sentinel_Fallback()
    {
        var managedCsproj = """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <PackageId>Janset.SDL2.Image</PackageId>
                <MinVerTagPrefix>sdl2-image-</MinVerTagPrefix>
                <Sdl2ImageFamilyVersion>$(Version)</Sdl2ImageFamilyVersion>
              </PropertyGroup>
              <ItemGroup>
                <ProjectReference Include="..\native\SDL2.Image.Native\SDL2.Image.Native.csproj" PrivateAssets="all" />
              </ItemGroup>
              <ItemGroup>
                <PackageVersion Include="Janset.SDL2.Image.Native" Version="[$(Sdl2ImageFamilyVersion)]" />
                <PackageReference Include="Janset.SDL2.Image.Native" />
              </ItemGroup>
            </Project>
            """;
        var (validator, repoRoot, manifest) = ArrangeWithFamily(
            (Path: "src/SDL2.Image/SDL2.Image.csproj", Content: managedCsproj),
            NativeCsproj("Janset.SDL2.Image.Native", "sdl2-image-"));

        var result = validator.Validate(manifest, repoRoot);

        await AssertFails(result, CsprojPackContractCheckKind.FamilyVersionPropertyHasSentinelFallback);
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

    private enum FamilyVersionShape
    {
        Canonical,
        MissingSentinel,
        Missing,
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

    private static (string Path, string Content) ManagedCsproj(string family, string packageId, string tagPrefix, FamilyVersionShape shape, bool includeNativeRef)
    {
        var familyVersionProperty = FamilyIdentifierConventions.FamilyVersionPropertyName(family);
        var nativePackageId = FamilyIdentifierConventions.NativePackageId(family);
        var familyVersionXml = shape switch
        {
            FamilyVersionShape.Canonical =>
                $"<{familyVersionProperty} Condition=\"'$(Version)' != '' and '$({familyVersionProperty})' == ''\">$(Version)</{familyVersionProperty}>\n    " +
                $"<{familyVersionProperty} Condition=\"'$({familyVersionProperty})' == ''\">0.0.0-restore</{familyVersionProperty}>",
            FamilyVersionShape.MissingSentinel =>
                $"<{familyVersionProperty}>$(Version)</{familyVersionProperty}>",
            _ => string.Empty,
        };

        var nativeRef = includeNativeRef
            ? @"<ProjectReference Include=""..\native\SDL2.Image.Native\SDL2.Image.Native.csproj"" PrivateAssets=""all"" />"
            : string.Empty;

        var content = $"""
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <PackageId>{packageId}</PackageId>
                <MinVerTagPrefix>{tagPrefix}</MinVerTagPrefix>
                {familyVersionXml}
              </PropertyGroup>
              <ItemGroup>
                {nativeRef}
              </ItemGroup>
              <ItemGroup>
                <PackageVersion Include="{nativePackageId}" Version="[$({familyVersionProperty})]" />
                <PackageReference Include="{nativePackageId}" />
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
