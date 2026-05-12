using System.IO.Compression;
using System.Text.Json;
using Build.Data.NativePackageMetadata;
using Build.Host.Cake;
using Build.Data.Manifest.Models;
using Build.Data.ProjectMetadata;
using Build.Results;
using Build.Targets.Package.Models;
using Build.Tests.Fixtures;
using Build.Validation.Conventions;
using Build.Validation.Packaging;
using Cake.Core.IO;

namespace Build.Tests.Unit.Validation.Packaging;

/// <summary>
/// Covers the current package guardrails: minimum-range dependency emission,
/// family coherence, and package payload/naming validation.
/// </summary>
public sealed class PackageOutputValidatorTests
{
    private const string ExpectedAuthors = "Janset2D, Deniz İrgin";
    private const string ExpectedLicenseFile = "LICENSE";
    private const string ExpectedIcon = "janset2d-sdl-min.png";
    private const string ExpectedCommit = "0123456789abcdef0123456789abcdef01234567";

    // Long-form TFMs emitted in nuspec dependency groups (what `dotnet pack` writes).
    private static readonly string[] NuspecFrameworkGroups = [".NETFramework4.6.2", ".NETStandard2.0", "net8.0", "net9.0", "net10.0"];

    // Short-form TFMs as resolved from MSBuild -getProperty:TargetFrameworks (what the reader returns).
    private static readonly string[] CsprojTargetFrameworks = ["net10.0", "net9.0", "net8.0", "netstandard2.0", "net462"];

    private static PackageOutputValidator CreateValidator(FakeCakeWorldV2 world)
        => new(
            world.FileSystem,
            new NativePackageMetadataValidator(new NativePackageMetadataRepository(world.CakeContext)),
            new ReadmeMappingTableValidator(world.FileSystem),
            new SatelliteUpperBoundValidator());

    [Test]
    public async Task Validate_Should_Pass_When_Artifacts_Conform_For_Satellite_Family()
    {
        var world = FakeCakeWorldV2.CreateWindows();
        var family = GetFamily("sdl2-image");
        var artifacts = CreateArtifacts(world, family, "1.2.3");
        var validator = CreateValidator(world);

        var report = await ValidateAsync(validator, world, family, artifacts, "1.2.3", DefaultMetadata());

        await Assert.That(report.IsValid).IsTrue();
        await Assert.That(report.Errors.Count).IsEqualTo(0);
    }

    [Test]
    public async Task Validate_Should_Fail_When_Native_Dependency_Is_Bracketed_Post_S1()
    {
        var world = FakeCakeWorldV2.CreateWindows();
        var family = GetFamily("sdl2-image");
        var artifacts = CreateArtifacts(world, family, "1.2.3", nativeDependencyVersion: "[1.2.3]");
        var validator = CreateValidator(world);

        var report = await ValidateAsync(validator, world, family, artifacts, "1.2.3", DefaultMetadata());

        await Assert.That(report.IsValid).IsFalse();
        await Assert.That(report.Errors.Any(check => check.Code == "G21")).IsTrue();
    }

    [Test]
    public async Task Validate_Should_Fail_When_CrossFamily_Dependency_Is_Bracketed()
    {
        var world = FakeCakeWorldV2.CreateWindows();
        var family = GetFamily("sdl2-image");
        var artifacts = CreateArtifacts(world, family, "1.2.3", coreDependencyVersion: "[1.2.3]");
        var validator = CreateValidator(world);

        var report = await ValidateAsync(validator, world, family, artifacts, "1.2.3", DefaultMetadata());

        await Assert.That(report.IsValid).IsFalse();
        await Assert.That(report.Errors.Any(check => check.Code == "G56")).IsTrue();
    }

    [Test]
    public async Task Validate_Should_Fail_When_Native_Dependency_Excludes_Build_Assets()
    {
        var world = FakeCakeWorldV2.CreateWindows();
        var family = GetFamily("sdl2-image");

        var dependencyGroups = CreateDependencyGroups(
            NuspecFrameworkGroups.Select(group => (group, CreateDependencyMap(
                family,
                "1.2.3",
                "1.2.3"))).ToList(),
            nativeDependencyMetadata: "exclude=\"Build,Analyzers\"");

        var artifacts = CreateArtifacts(
            world,
            family,
            "1.2.3",
            managedDependencyGroupsXml: dependencyGroups);

        var validator = CreateValidator(world);

        var report = await ValidateAsync(validator, world, family, artifacts, "1.2.3", DefaultMetadata());

        await Assert.That(report.IsValid).IsFalse();
        await Assert.That(report.Errors.Any(check => check.Code == "G21")).IsTrue();
    }

    [Test]
    public async Task Validate_Should_Fail_When_Managed_And_Native_Versions_Drift()
    {
        // Detect mismatched family members that would otherwise resolve silently under
        // the minimum-range contract.
        var world = FakeCakeWorldV2.CreateWindows();
        var family = GetFamily("sdl2-image");
        var artifacts = CreateArtifacts(world, family, "1.2.3", nativePackageVersion: "1.2.4");
        var validator = CreateValidator(world);

        var report = await ValidateAsync(validator, world, family, artifacts, "1.2.3", DefaultMetadata());

        await Assert.That(report.IsValid).IsFalse();
        await Assert.That(report.Errors.Any(check => check.Code == "G23")).IsTrue();
    }

    [Test]
    public async Task Validate_Should_Fail_When_TargetFramework_Groups_Are_Inconsistent()
    {
        var world = FakeCakeWorldV2.CreateWindows();
        var family = GetFamily("sdl2-image");

        var dependencyGroups = CreateDependencyGroups(
            NuspecFrameworkGroups.Select(group =>
                group == "net10.0"
                    ? (group, CreateDependencyMap(family, "1.2.3", "9.9.9"))
                    : (group, CreateDependencyMap(family, "1.2.3", "1.2.3")))
                .ToList());

        var artifacts = CreateArtifacts(
            world,
            family,
            "1.2.3",
            managedDependencyGroupsXml: dependencyGroups);

        var validator = CreateValidator(world);

        var report = await ValidateAsync(validator, world, family, artifacts, "1.2.3", DefaultMetadata());

        await Assert.That(report.IsValid).IsFalse();
        await Assert.That(report.Errors.Any(check => check.Code == "G22")).IsTrue();
    }

    [Test]
    public async Task Validate_Should_Fail_When_Managed_Symbol_Package_Is_Missing()
    {
        var world = FakeCakeWorldV2.CreateWindows();
        var family = GetFamily("sdl2-image");
        var artifacts = CreateArtifacts(world, family, "1.2.3", includeSymbols: false);
        var validator = CreateValidator(world);

        var report = await ValidateAsync(validator, world, family, artifacts, "1.2.3", DefaultMetadata());

        await Assert.That(report.IsValid).IsFalse();
        await Assert.That(report.Errors.Any(check => check.Code == "G25")).IsTrue();
    }

    [Test]
    public async Task Validate_Should_Fail_When_Repository_Commit_Drifts()
    {
        var world = FakeCakeWorldV2.CreateWindows();
        var family = GetFamily("sdl2-image");
        var artifacts = CreateArtifacts(world, family, "1.2.3", commit: "deadbeefdeadbeefdeadbeefdeadbeefdeadbeef");
        var validator = CreateValidator(world);

        var report = await ValidateAsync(validator, world, family, artifacts, "1.2.3", DefaultMetadata());

        await Assert.That(report.IsValid).IsFalse();
        await Assert.That(report.Errors.Any(check => check.Code == "G26")).IsTrue();
    }

    [Test]
    public async Task Validate_Should_Fail_When_Nuspec_Authors_Differ_From_Csproj_Metadata()
    {
        var world = FakeCakeWorldV2.CreateWindows();
        var family = GetFamily("sdl2-image");
        var artifacts = CreateArtifacts(world, family, "1.2.3", authors: "Wrong Author");
        var validator = CreateValidator(world);

        var report = await ValidateAsync(validator, world, family, artifacts, "1.2.3", DefaultMetadata());

        await Assert.That(report.IsValid).IsFalse();
        await Assert.That(report.Errors.Any(check => check.Code == "G27")).IsTrue();
    }

    [Test]
    public async Task Validate_Should_Fail_When_Csproj_TargetFrameworks_Diverge_From_Nuspec_Groups()
    {
        var world = FakeCakeWorldV2.CreateWindows();
        var family = GetFamily("sdl2-image");
        var artifacts = CreateArtifacts(world, family, "1.2.3");
        var validator = CreateValidator(world);

        var metadataMissingNet462 = DefaultMetadata(targetFrameworks: ["net10.0", "net9.0", "net8.0", "netstandard2.0"]);

        var report = await ValidateAsync(validator, world, family, artifacts, "1.2.3", metadataMissingNet462);

        await Assert.That(report.IsValid).IsFalse();
        await Assert.That(report.Errors.Any(check => check.Code == "G22")).IsTrue();
    }

    [Test]
    public async Task Validate_Should_Fail_When_Native_Package_Missing_BuildTransitive_Wrapper()
    {
        var world = FakeCakeWorldV2.CreateWindows();
        var family = GetFamily("sdl2-image");
        var artifacts = CreateArtifacts(world, family, "1.2.3", nativeIncludeBuildTransitiveWrapper: false);
        var validator = CreateValidator(world);

        var report = await ValidateAsync(validator, world, family, artifacts, "1.2.3", DefaultMetadata());

        await Assert.That(report.IsValid).IsFalse();
        await Assert.That(report.Errors.Any(check => check.Code == "G47")).IsTrue();
    }

    [Test]
    public async Task Validate_Should_Fail_When_Native_Package_Missing_Shared_Common_Targets()
    {
        var world = FakeCakeWorldV2.CreateWindows();
        var family = GetFamily("sdl2-image");
        var artifacts = CreateArtifacts(world, family, "1.2.3", nativeIncludeSharedCommonTargets: false);
        var validator = CreateValidator(world);

        var report = await ValidateAsync(validator, world, family, artifacts, "1.2.3", DefaultMetadata());

        await Assert.That(report.IsValid).IsFalse();
        await Assert.That(report.Errors.Any(check => check.Code == "G47")).IsTrue();
    }

    [Test]
    public async Task Validate_Should_Fail_When_Native_Package_Has_No_License_Payload()
    {
        // G51: native package must ship at least one entry under licenses/. Last line of
        // defence against the H1 failure mode — if upstream invalidation + gate are
        // bypassed, this post-pack check catches a nupkg shipped without third-party
        // attribution.
        var world = FakeCakeWorldV2.CreateWindows();
        var family = GetFamily("sdl2-image");
        var artifacts = CreateArtifacts(world, family, "1.2.3", nativeIncludeLicensePayload: false);
        var validator = CreateValidator(world);

        var report = await ValidateAsync(validator, world, family, artifacts, "1.2.3", DefaultMetadata());

        await Assert.That(report.IsValid).IsFalse();
        await Assert.That(report.Errors.Any(check => check.Code == "G51")).IsTrue();
    }

    [Test]
    public async Task Validate_Should_Fail_When_Native_Package_Linux_Tarball_Has_Wrong_Name()
    {
        // A generic archive name would collide with sibling .Native packages on the
        // consumer side. Validator must catch any drift away from $(PackageId).tar.gz naming.
        var world = FakeCakeWorldV2.CreateWindows();
        var family = GetFamily("sdl2-image");
        var artifacts = CreateArtifacts(world, family, "1.2.3", nativeLinuxTarballFileName: "native.tar.gz");
        var validator = CreateValidator(world);

        var report = await ValidateAsync(validator, world, family, artifacts, "1.2.3", DefaultMetadata());

        await Assert.That(report.IsValid).IsFalse();
        await Assert.That(report.Errors.Any(check => check.Code == "G48")).IsTrue();
    }

    [Test]
    public async Task Validate_Should_Fail_When_Project_Metadata_License_Is_Empty()
    {
        var world = FakeCakeWorldV2.CreateWindows();
        var family = GetFamily("sdl2-image");
        var artifacts = CreateArtifacts(world, family, "1.2.3");
        var validator = CreateValidator(world);

        var metadata = new EvaluatedProjectMetadata(
            TargetFrameworks: CsprojTargetFrameworks,
            Authors: ExpectedAuthors,
            PackageLicenseFile: string.Empty,
            PackageIcon: ExpectedIcon);

        var report = await ValidateAsync(validator, world, family, artifacts, "1.2.3", metadata);

        await Assert.That(report.IsValid).IsFalse();
        await Assert.That(report.Errors.Any(check => check.Name == "Project metadata completeness")).IsTrue();
    }

    private static EvaluatedProjectMetadata DefaultMetadata(
        IReadOnlyList<string>? targetFrameworks = null,
        string? authors = null,
        string? licenseFile = null,
        string? icon = null)
    {
        return new EvaluatedProjectMetadata(
            TargetFrameworks: targetFrameworks ?? CsprojTargetFrameworks,
            Authors: authors ?? ExpectedAuthors,
            PackageLicenseFile: licenseFile ?? ExpectedLicenseFile,
            PackageIcon: icon ?? ExpectedIcon);
    }

    private static PackageFamilyConfig GetFamily(string familyName)
    {
        return ManifestFixture.CreateTestManifestConfig().PackageFamilies.Single(family => string.Equals(family.Name, familyName, StringComparison.OrdinalIgnoreCase));
    }

    private static async Task<ValidationReport> ValidateAsync(
        PackageOutputValidator validator,
        FakeCakeWorldV2 world,
        PackageFamilyConfig family,
        PackageArtifacts artifacts,
        string expectedVersion,
        EvaluatedProjectMetadata metadata)
    {
        var manifest = ManifestFixture.CreateTestManifestConfig();
        var readmePath = EnsureReadme(world, manifest);

        return await validator.ValidateAsync(
            family,
            artifacts,
            expectedVersion,
            ExpectedCommit,
            metadata,
            manifest,
            readmePath);
    }

    private static FilePath EnsureReadme(FakeCakeWorldV2 world, ManifestConfig manifest)
    {
        var readmePath = world.RepoRoot.CombineWithFilePath("README.md");
        world.WithTextFile(readmePath, ReadmeMappingTableBlock.BuildBlock(manifest));
        return readmePath;
    }

    private static PackageArtifacts CreateArtifacts(
        FakeCakeWorldV2 world,
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
        bool nativeIncludeMetadataFile = true,
        string? nativeLinuxTarballFileName = null)
    {
        var managedPackageId = FamilyIdentifierConventions.ManagedPackageId(family.Name);
        var nativePackageId = FamilyIdentifierConventions.NativePackageId(family.Name);

        var managedPackagePath = world.RepoRoot.CombineWithFilePath($"artifacts/packages/{managedPackageId}.{version}.nupkg");
        var nativePackagePath = world.RepoRoot.CombineWithFilePath($"artifacts/packages/{nativePackageId}.{version}.nupkg");
        var symbolsPackagePath = world.RepoRoot.CombineWithFilePath($"artifacts/packages/{managedPackageId}.{version}.snupkg");

        var dependencyGroups = managedDependencyGroupsXml ?? CreateDependencyGroups(
            NuspecFrameworkGroups.Select(group => (group, CreateDependencyMap(
                family,
                nativeDependencyVersion ?? version,
                coreDependencyVersion ?? BuildCrossFamilyRangeExpression(family, version)))).ToList());

        WriteZip(
            world,
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

        if (nativeIncludeMetadataFile)
        {
            nativeEntries.Add(("janset-native-metadata.json", CreateNativeMetadataJson(family, version, commit ?? ExpectedCommit)));
        }

        nativeEntries.Add(("runtimes/win-x64/native/SDL2.dll", "win-x64-dll"));

        WriteZip(world, nativePackagePath, [.. nativeEntries]);

        if (includeSymbols)
        {
            WriteZip(
                world,
                symbolsPackagePath,
                ("symbols.nuspec", "<package><metadata><id>symbols</id></metadata></package>"),
                ($"lib/net10.0/{managedPackageId}.pdb", "pdb"));
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

    private static string BuildCrossFamilyRangeExpression(PackageFamilyConfig family, string lowerBoundVersion)
    {
        ArgumentNullException.ThrowIfNull(family);
        ArgumentException.ThrowIfNullOrWhiteSpace(lowerBoundVersion);

        var manifest = ManifestFixture.CreateTestManifestConfig();
        var coreFamily = manifest.PackageFamilies.Single(config => string.Equals(config.Name, family.DependsOn[0], StringComparison.OrdinalIgnoreCase));
        var coreLibrary = manifest.LibraryManifests.Single(library => string.Equals(library.Name, coreFamily.LibraryRef, StringComparison.OrdinalIgnoreCase));

        if (!NuGet.Versioning.NuGetVersion.TryParse(coreLibrary.VcpkgVersion, out var coreUpstreamVersion))
        {
            throw new InvalidOperationException($"Invalid test fixture core vcpkg version '{coreLibrary.VcpkgVersion}'.");
        }

        var upperBound = new NuGet.Versioning.NuGetVersion(coreUpstreamVersion.Major + 1, 0, 0);
        return $"[{lowerBoundVersion}, {upperBound})";
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

    private static string CreateNativeMetadataJson(PackageFamilyConfig family, string familyVersion, string commit)
    {
        ArgumentNullException.ThrowIfNull(family);
        ArgumentException.ThrowIfNullOrWhiteSpace(familyVersion);
        ArgumentException.ThrowIfNullOrWhiteSpace(commit);

        var manifest = ManifestFixture.CreateTestManifestConfig();
        var library = manifest.LibraryManifests.Single(l => string.Equals(l.Name, family.LibraryRef, StringComparison.OrdinalIgnoreCase));

        var metadata = new NativePackageMetadataDocument
        {
            JansetFamilyVersion = familyVersion,
            FamilyIdentifier = family.Name,
            UpstreamLibrary = library.VcpkgName,
            UpstreamVersion = library.VcpkgVersion,
            VcpkgPortVersion = library.VcpkgPortVersion,
            TripletSet = manifest.Runtimes
                .Select(runtime => runtime.Triplet)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(triplet => triplet, StringComparer.OrdinalIgnoreCase)
                .ToList(),
            BuildCommit = commit,
        };

        return JsonSerializer.Serialize(metadata, CakeJsonExtensions.DefaultJsonOptions);
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

    private static void WriteZip(FakeCakeWorldV2 world, FilePath archivePath, params (string EntryName, string Content)[] entries)
    {
        var directory = world.FileSystem.GetDirectory(archivePath.GetDirectory());
        if (!directory.Exists)
        {
            directory.Create();
        }

        var file = world.FileSystem.GetFile(archivePath);
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
