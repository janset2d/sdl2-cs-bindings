using System.IO.Compression;
using Build.Context.Models;
using Build.Modules.Packaging;
using Build.Modules.Packaging.Models;
using Build.Modules.Preflight;
using Build.Tests.Fixtures;
using Cake.Core.IO;
using OneOf.Monads;

namespace Build.Tests.Unit.Modules.Packaging;

/// <summary>
/// Post-S1 scope: G20/G24 retired. G21 unified (all family deps = minimum range),
/// G23 promoted to primary within-family coherence check. G22/G25/G26/G27 unchanged.
/// </summary>
public sealed class PackageOutputValidatorTests
{
    private const string ExpectedAuthors = "Janset2D, Deniz \u0130rgin";
    private const string ExpectedLicenseFile = "LICENSE";
    private const string ExpectedIcon = "janset2d-sdl-min.png";
    private const string ExpectedCommit = "0123456789abcdef0123456789abcdef01234567";

    // Long-form TFMs emitted in nuspec dependency groups (what `dotnet pack` writes).
    private static readonly string[] NuspecFrameworkGroups = [".NETFramework4.6.2", ".NETStandard2.0", "net8.0", "net9.0"];

    // Short-form TFMs as resolved from MSBuild -getProperty:TargetFrameworks (what the reader returns).
    private static readonly string[] CsprojTargetFrameworks = ["net9.0", "net8.0", "netstandard2.0", "net462"];

    [Test]
    public async Task Validate_Should_Pass_When_Artifacts_Conform_For_Satellite_Family()
    {
        var repo = new FakeRepoBuilder(FakeRepoPlatform.Windows).BuildContextWithHandles();
        var family = GetFamily("sdl2-image");
        var artifacts = CreateArtifacts(repo, family, "1.2.3");
        var validator = new PackageOutputValidator(repo.FileSystem);

        var result = await validator.ValidateAsync(family, artifacts, "1.2.3", ExpectedCommit, DefaultMetadata());

        await Assert.That(result.IsSuccess()).IsTrue();
        await Assert.That(result.Validation.HasErrors).IsFalse();
    }

    [Test]
    public async Task Validate_Should_Fail_When_Native_Dependency_Is_Bracketed_Post_S1()
    {
        var repo = new FakeRepoBuilder(FakeRepoPlatform.Windows).BuildContextWithHandles();
        var family = GetFamily("sdl2-image");
        var artifacts = CreateArtifacts(repo, family, "1.2.3", nativeDependencyVersion: "[1.2.3]");
        var validator = new PackageOutputValidator(repo.FileSystem);

        var result = await validator.ValidateAsync(family, artifacts, "1.2.3", ExpectedCommit, DefaultMetadata());

        await Assert.That(result.IsError()).IsTrue();
        await Assert.That(result.Validation.Checks.Any(check => check.Kind == PackageValidationCheckKind.FamilyDependencyMinimumRange && check.IsError)).IsTrue();
    }

    [Test]
    public async Task Validate_Should_Fail_When_CrossFamily_Dependency_Is_Bracketed()
    {
        var repo = new FakeRepoBuilder(FakeRepoPlatform.Windows).BuildContextWithHandles();
        var family = GetFamily("sdl2-image");
        var artifacts = CreateArtifacts(repo, family, "1.2.3", coreDependencyVersion: "[1.2.3]");
        var validator = new PackageOutputValidator(repo.FileSystem);

        var result = await validator.ValidateAsync(family, artifacts, "1.2.3", ExpectedCommit, DefaultMetadata());

        await Assert.That(result.IsError()).IsTrue();
        await Assert.That(result.Validation.Checks.Any(check => check.Kind == PackageValidationCheckKind.FamilyDependencyMinimumRange && check.IsError)).IsTrue();
    }

    [Test]
    public async Task Validate_Should_Fail_When_Native_Dependency_Excludes_Build_Assets()
    {
        var repo = new FakeRepoBuilder(FakeRepoPlatform.Windows).BuildContextWithHandles();
        var family = GetFamily("sdl2-image");

        var dependencyGroups = CreateDependencyGroups(
            NuspecFrameworkGroups.Select(group => (group, CreateDependencyMap(
                family,
                "1.2.3",
                "1.2.3"))).ToList(),
            nativeDependencyMetadata: "exclude=\"Build,Analyzers\"");

        var artifacts = CreateArtifacts(
            repo,
            family,
            "1.2.3",
            managedDependencyGroupsXml: dependencyGroups);

        var validator = new PackageOutputValidator(repo.FileSystem);

        var result = await validator.ValidateAsync(family, artifacts, "1.2.3", ExpectedCommit, DefaultMetadata());

        await Assert.That(result.IsError()).IsTrue();
        await Assert.That(result.Validation.Checks.Any(check => check.Kind == PackageValidationCheckKind.FamilyDependencyMinimumRange && check.IsError)).IsTrue();
    }

    [Test]
    public async Task Validate_Should_Fail_When_Managed_And_Native_Versions_Drift()
    {
        // G23 primary check post-S1: detects mismatched family members that would otherwise
        // resolve silently under the minimum-range contract.
        var repo = new FakeRepoBuilder(FakeRepoPlatform.Windows).BuildContextWithHandles();
        var family = GetFamily("sdl2-image");
        var artifacts = CreateArtifacts(repo, family, "1.2.3", nativePackageVersion: "1.2.4");
        var validator = new PackageOutputValidator(repo.FileSystem);

        var result = await validator.ValidateAsync(family, artifacts, "1.2.3", ExpectedCommit, DefaultMetadata());

        await Assert.That(result.IsError()).IsTrue();
        await Assert.That(result.Validation.Checks.Any(check => check.Kind == PackageValidationCheckKind.WithinFamilyVersionCoherence && check.IsError)).IsTrue();
    }

    [Test]
    public async Task Validate_Should_Fail_When_TargetFramework_Groups_Are_Inconsistent()
    {
        var repo = new FakeRepoBuilder(FakeRepoPlatform.Windows).BuildContextWithHandles();
        var family = GetFamily("sdl2-image");

        var dependencyGroups = CreateDependencyGroups(
            NuspecFrameworkGroups.Select(group =>
                group == "net9.0"
                    ? (group, CreateDependencyMap(family, "1.2.3", "9.9.9"))
                    : (group, CreateDependencyMap(family, "1.2.3", "1.2.3")))
                .ToList());

        var artifacts = CreateArtifacts(
            repo,
            family,
            "1.2.3",
            managedDependencyGroupsXml: dependencyGroups);

        var validator = new PackageOutputValidator(repo.FileSystem);

        var result = await validator.ValidateAsync(family, artifacts, "1.2.3", ExpectedCommit, DefaultMetadata());

        await Assert.That(result.IsError()).IsTrue();
        await Assert.That(result.Validation.Checks.Any(check => check.Kind == PackageValidationCheckKind.DependencyGroupsConsistentAcrossFrameworks && check.IsError)).IsTrue();
    }

    [Test]
    public async Task Validate_Should_Fail_When_Managed_Symbol_Package_Is_Missing()
    {
        var repo = new FakeRepoBuilder(FakeRepoPlatform.Windows).BuildContextWithHandles();
        var family = GetFamily("sdl2-image");
        var artifacts = CreateArtifacts(repo, family, "1.2.3", includeSymbols: false);
        var validator = new PackageOutputValidator(repo.FileSystem);

        var result = await validator.ValidateAsync(family, artifacts, "1.2.3", ExpectedCommit, DefaultMetadata());

        await Assert.That(result.IsError()).IsTrue();
        await Assert.That(result.Validation.Checks.Any(check => check.Kind == PackageValidationCheckKind.ManagedSymbolsPackageValid && check.IsError)).IsTrue();
    }

    [Test]
    public async Task Validate_Should_Fail_When_Repository_Commit_Drifts()
    {
        var repo = new FakeRepoBuilder(FakeRepoPlatform.Windows).BuildContextWithHandles();
        var family = GetFamily("sdl2-image");
        var artifacts = CreateArtifacts(repo, family, "1.2.3", commit: "deadbeefdeadbeefdeadbeefdeadbeefdeadbeef");
        var validator = new PackageOutputValidator(repo.FileSystem);

        var result = await validator.ValidateAsync(family, artifacts, "1.2.3", ExpectedCommit, DefaultMetadata());

        await Assert.That(result.IsError()).IsTrue();
        await Assert.That(result.Validation.Checks.Any(check => check.Kind == PackageValidationCheckKind.CanonicalMetadataMatches && check.IsError)).IsTrue();
    }

    [Test]
    public async Task Validate_Should_Fail_When_Nuspec_Authors_Differ_From_Csproj_Metadata()
    {
        var repo = new FakeRepoBuilder(FakeRepoPlatform.Windows).BuildContextWithHandles();
        var family = GetFamily("sdl2-image");
        var artifacts = CreateArtifacts(repo, family, "1.2.3", authors: "Wrong Author");
        var validator = new PackageOutputValidator(repo.FileSystem);

        var result = await validator.ValidateAsync(family, artifacts, "1.2.3", ExpectedCommit, DefaultMetadata());

        await Assert.That(result.IsError()).IsTrue();
        await Assert.That(result.Validation.Checks.Any(check => check.Kind == PackageValidationCheckKind.CanonicalMetadataMatches && check.IsError)).IsTrue();
    }

    [Test]
    public async Task Validate_Should_Fail_When_Csproj_TargetFrameworks_Diverge_From_Nuspec_Groups()
    {
        var repo = new FakeRepoBuilder(FakeRepoPlatform.Windows).BuildContextWithHandles();
        var family = GetFamily("sdl2-image");
        var artifacts = CreateArtifacts(repo, family, "1.2.3");
        var validator = new PackageOutputValidator(repo.FileSystem);

        var metadataMissingNet462 = DefaultMetadata(targetFrameworks: ["net9.0", "net8.0", "netstandard2.0"]);

        var result = await validator.ValidateAsync(family, artifacts, "1.2.3", ExpectedCommit, metadataMissingNet462);

        await Assert.That(result.IsError()).IsTrue();
        await Assert.That(result.Validation.Checks.Any(check => check.Kind == PackageValidationCheckKind.DependencyGroupsConsistentAcrossFrameworks && check.IsError)).IsTrue();
    }

    [Test]
    public async Task Validate_Should_Fail_When_Native_Package_Missing_BuildTransitive_Wrapper()
    {
        var repo = new FakeRepoBuilder(FakeRepoPlatform.Windows).BuildContextWithHandles();
        var family = GetFamily("sdl2-image");
        var artifacts = CreateArtifacts(repo, family, "1.2.3", nativeIncludeBuildTransitiveWrapper: false);
        var validator = new PackageOutputValidator(repo.FileSystem);

        var result = await validator.ValidateAsync(family, artifacts, "1.2.3", ExpectedCommit, DefaultMetadata());

        await Assert.That(result.IsError()).IsTrue();
        await Assert.That(result.Validation.Checks.Any(check => check.Kind == PackageValidationCheckKind.BuildTransitiveContractPresent && check.IsError)).IsTrue();
    }

    [Test]
    public async Task Validate_Should_Fail_When_Native_Package_Missing_Shared_Common_Targets()
    {
        var repo = new FakeRepoBuilder(FakeRepoPlatform.Windows).BuildContextWithHandles();
        var family = GetFamily("sdl2-image");
        var artifacts = CreateArtifacts(repo, family, "1.2.3", nativeIncludeSharedCommonTargets: false);
        var validator = new PackageOutputValidator(repo.FileSystem);

        var result = await validator.ValidateAsync(family, artifacts, "1.2.3", ExpectedCommit, DefaultMetadata());

        await Assert.That(result.IsError()).IsTrue();
        await Assert.That(result.Validation.Checks.Any(check => check.Kind == PackageValidationCheckKind.BuildTransitiveContractPresent && check.IsError)).IsTrue();
    }

    [Test]
    public async Task Validate_Should_Fail_When_Native_Package_Has_No_License_Payload()
    {
        // G51: native package must ship at least one entry under licenses/. Last line of
        // defence against the H1 failure mode — if upstream invalidation + gate are
        // bypassed, this post-pack check catches a nupkg shipped without third-party
        // attribution.
        var repo = new FakeRepoBuilder(FakeRepoPlatform.Windows).BuildContextWithHandles();
        var family = GetFamily("sdl2-image");
        var artifacts = CreateArtifacts(repo, family, "1.2.3", nativeIncludeLicensePayload: false);
        var validator = new PackageOutputValidator(repo.FileSystem);

        var result = await validator.ValidateAsync(family, artifacts, "1.2.3", ExpectedCommit, DefaultMetadata());

        await Assert.That(result.IsError()).IsTrue();
        await Assert.That(result.Validation.Checks.Any(check => check.Kind == PackageValidationCheckKind.LicensePayloadPresent && check.IsError)).IsTrue();
    }

    [Test]
    public async Task Validate_Should_Fail_When_Native_Package_Linux_Tarball_Has_Wrong_Name()
    {
        // G29: an archive named native.tar.gz (the pre-S1 shape) would collide with
        // sibling .Native packages on the consumer side. Validator must catch any drift
        // away from $(PackageId).tar.gz naming.
        var repo = new FakeRepoBuilder(FakeRepoPlatform.Windows).BuildContextWithHandles();
        var family = GetFamily("sdl2-image");
        var artifacts = CreateArtifacts(repo, family, "1.2.3", nativeLinuxTarballFileName: "native.tar.gz");
        var validator = new PackageOutputValidator(repo.FileSystem);

        var result = await validator.ValidateAsync(family, artifacts, "1.2.3", ExpectedCommit, DefaultMetadata());

        await Assert.That(result.IsError()).IsTrue();
        await Assert.That(result.Validation.Checks.Any(check => check.Kind == PackageValidationCheckKind.NativePayloadShapePerRid && check.IsError)).IsTrue();
    }

    [Test]
    public async Task Validate_Should_Fail_When_Project_Metadata_License_Is_Empty()
    {
        var repo = new FakeRepoBuilder(FakeRepoPlatform.Windows).BuildContextWithHandles();
        var family = GetFamily("sdl2-image");
        var artifacts = CreateArtifacts(repo, family, "1.2.3");
        var validator = new PackageOutputValidator(repo.FileSystem);

        var metadata = new ProjectMetadata(
            TargetFrameworks: CsprojTargetFrameworks,
            Authors: ExpectedAuthors,
            PackageLicenseFile: string.Empty,
            PackageIcon: ExpectedIcon);

        var result = await validator.ValidateAsync(family, artifacts, "1.2.3", ExpectedCommit, metadata);

        await Assert.That(result.IsError()).IsTrue();
        await Assert.That(result.Validation.Checks.Any(check => check.Kind == PackageValidationCheckKind.ProjectMetadataComplete && check.IsError)).IsTrue();
    }

    private static ProjectMetadata DefaultMetadata(
        IReadOnlyList<string>? targetFrameworks = null,
        string? authors = null,
        string? licenseFile = null,
        string? icon = null)
    {
        return new ProjectMetadata(
            TargetFrameworks: targetFrameworks ?? CsprojTargetFrameworks,
            Authors: authors ?? ExpectedAuthors,
            PackageLicenseFile: licenseFile ?? ExpectedLicenseFile,
            PackageIcon: icon ?? ExpectedIcon);
    }

    private static PackageFamilyConfig GetFamily(string familyName)
    {
        return ManifestFixture.CreateTestManifestConfig().PackageFamilies.Single(family => string.Equals(family.Name, familyName, StringComparison.OrdinalIgnoreCase));
    }

    private static PackageArtifacts CreateArtifacts(
        FakeRepoHandles repo,
        PackageFamilyConfig family,
        string version,
        string? nativeDependencyVersion = null,
        string? coreDependencyVersion = null,
        string? managedDependencyGroupsXml = null,
        string? nativePackageVersion = null,
        string? commit = null,
        string? authors = null,
        bool includeSymbols = true,
        bool nativeIncludeBuildTransitiveWrapper = true,
        bool nativeIncludeSharedCommonTargets = true,
        bool nativeIncludeLicensePayload = true,
        string? nativeLinuxTarballFileName = null)
    {
        var managedPackageId = FamilyIdentifierConventions.ManagedPackageId(family.Name);
        var nativePackageId = FamilyIdentifierConventions.NativePackageId(family.Name);

        var managedPackagePath = repo.ResolveFile($"artifacts/packages/{managedPackageId}.{version}.nupkg");
        var nativePackagePath = repo.ResolveFile($"artifacts/packages/{nativePackageId}.{version}.nupkg");
        var symbolsPackagePath = repo.ResolveFile($"artifacts/packages/{managedPackageId}.{version}.snupkg");

        var dependencyGroups = managedDependencyGroupsXml ?? CreateDependencyGroups(
            NuspecFrameworkGroups.Select(group => (group, CreateDependencyMap(
                family,
                nativeDependencyVersion ?? version,
                coreDependencyVersion ?? version))).ToList());

        WriteZip(
            repo,
            managedPackagePath,
            ("package.nuspec", CreateManagedNuspec(
                family,
                version,
                dependencyGroups,
                commit ?? ExpectedCommit,
                authors ?? ExpectedAuthors)));

        var nativeEntries = new List<(string EntryName, string Content)>
        {
            ("package.nuspec", CreateNativeNuspec(
                family,
                nativePackageVersion ?? version,
                commit ?? ExpectedCommit,
                authors ?? ExpectedAuthors)),
            ($"runtimes/linux-x64/native/{nativeLinuxTarballFileName ?? $"{nativePackageId}.tar.gz"}", "linux-tarball"),
            ($"runtimes/osx-x64/native/{nativePackageId}.tar.gz", "osx-tarball"),
        };

        if (nativeIncludeBuildTransitiveWrapper)
        {
            nativeEntries.Add(($"buildTransitive/{nativePackageId}.targets", "<Project />"));
        }

        if (nativeIncludeSharedCommonTargets)
        {
            nativeEntries.Add(("buildTransitive/Janset.SDL2.Native.Common.targets", "<Project />"));
        }

        if (nativeIncludeLicensePayload)
        {
            // G51: native package ships at least one third-party license entry. Default on
            // so existing "happy-path" tests stay green; negative tests opt out explicitly.
            nativeEntries.Add(("licenses/sdl2/copyright", "SDL2 zlib license text"));
        }

        nativeEntries.Add(("runtimes/win-x64/native/SDL2.dll", "win-x64-dll"));

        WriteZip(repo, nativePackagePath, nativeEntries.ToArray());

        if (includeSymbols)
        {
            WriteZip(
                repo,
                symbolsPackagePath,
                ("symbols.nuspec", "<package><metadata><id>symbols</id></metadata></package>"),
                ($"lib/net9.0/{managedPackageId}.pdb", "pdb"));
        }

        return new PackageArtifacts(managedPackagePath, symbolsPackagePath, nativePackagePath);
    }

    private static Dictionary<string, string> CreateDependencyMap(PackageFamilyConfig family, string nativeVersion, string? coreVersion)
    {
        var dependencyMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            [FamilyIdentifierConventions.NativePackageId(family.Name)] = nativeVersion,
        };

        foreach (var dependencyFamily in family.DependsOn)
        {
            dependencyMap[FamilyIdentifierConventions.ManagedPackageId(dependencyFamily)] = coreVersion ?? throw new InvalidOperationException("Expected a cross-family dependency version.");
        }

        return dependencyMap;
    }

    private static string CreateDependencyGroups(
        IReadOnlyList<(string Framework, Dictionary<string, string> Versions)> groups,
        string nativeDependencyMetadata = "include=\"All\"",
        string crossFamilyDependencyMetadata = "exclude=\"Build,Analyzers\"")
    {
        var groupXml = string.Join(
            Environment.NewLine,
            groups.Select(group =>
                $"      <group targetFramework=\"{group.Framework}\">{Environment.NewLine}" +
                string.Join(
                    Environment.NewLine,
                    group.Versions.Select(pair =>
                    {
                        var metadata = pair.Key.EndsWith(".Native", StringComparison.OrdinalIgnoreCase)
                            ? nativeDependencyMetadata
                            : crossFamilyDependencyMetadata;

                        return $"        <dependency id=\"{pair.Key}\" version=\"{pair.Value}\" {metadata} />";
                    })) +
                $"{Environment.NewLine}      </group>"));

        return groupXml;
    }

    private static string CreateManagedNuspec(
        PackageFamilyConfig family,
        string version,
        string dependencyGroupsXml,
        string commit,
        string authors)
    {
        return $"""
            <?xml version="1.0" encoding="utf-8"?>
            <package xmlns="http://schemas.microsoft.com/packaging/2013/05/nuspec.xsd">
              <metadata>
                <id>{FamilyIdentifierConventions.ManagedPackageId(family.Name)}</id>
                <version>{version}</version>
                <authors>{authors}</authors>
                <license type="file">LICENSE</license>
                <icon>janset2d-sdl-min.png</icon>
                <repository type="git" url="https://github.com/janset2d/sdl2-cs-bindings" commit="{commit}" />
                <dependencies>
            {dependencyGroupsXml}
                </dependencies>
              </metadata>
            </package>
            """;
    }

    private static string CreateNativeNuspec(
        PackageFamilyConfig family,
        string version,
        string commit,
        string authors)
    {
        return $"""
            <?xml version="1.0" encoding="utf-8"?>
            <package xmlns="http://schemas.microsoft.com/packaging/2013/05/nuspec.xsd">
              <metadata>
                <id>{FamilyIdentifierConventions.NativePackageId(family.Name)}</id>
                <version>{version}</version>
                <authors>{authors}</authors>
                <license type="file">LICENSE</license>
                <icon>janset2d-sdl-min.png</icon>
                <repository type="git" url="https://github.com/janset2d/sdl2-cs-bindings" commit="{commit}" />
              </metadata>
            </package>
            """;
    }

    private static void WriteZip(FakeRepoHandles repo, FilePath archivePath, params (string EntryName, string Content)[] entries)
    {
        var directory = repo.FileSystem.GetDirectory(archivePath.GetDirectory());
        if (!directory.Exists)
        {
            directory.Create();
        }

        var file = repo.FileSystem.GetFile(archivePath);
        using var stream = file.Open(FileMode.Create, FileAccess.Write, FileShare.None);
        using var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: false);

        foreach (var (entryName, content) in entries)
        {
            var entry = archive.CreateEntry(entryName);
            using var writer = new StreamWriter(entry.Open());
            writer.Write(content);
        }
    }
}
