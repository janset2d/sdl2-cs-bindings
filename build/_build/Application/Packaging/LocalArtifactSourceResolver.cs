using System.Globalization;
using System.Xml.Linq;
using Build.Context;
using Build.Context.Configs;
using Build.Context.Models;
using Build.Domain.Packaging.Models;
using Build.Domain.Paths;
using Build.Domain.Preflight;
using Build.Infrastructure.Paths;
using Cake.Common.IO;
using Cake.Core;
using Cake.Core.Diagnostics;
using Cake.Core.IO;
using NuGet.Versioning;

namespace Build.Application.Packaging;

public sealed class LocalArtifactSourceResolver(
    ICakeContext cakeContext,
    ICakeLog log,
    IPathService pathService,
    ManifestConfig manifestConfig,
    IPackageTaskRunner packageTaskRunner) : IArtifactSourceResolver
{
    private readonly ICakeContext _cakeContext = cakeContext ?? throw new ArgumentNullException(nameof(cakeContext));
    private readonly ICakeLog _log = log ?? throw new ArgumentNullException(nameof(log));
    private readonly IPathService _pathService = pathService ?? throw new ArgumentNullException(nameof(pathService));
    private readonly ManifestConfig _manifestConfig = manifestConfig ?? throw new ArgumentNullException(nameof(manifestConfig));
    private readonly IPackageTaskRunner _packageTaskRunner = packageTaskRunner ?? throw new ArgumentNullException(nameof(packageTaskRunner));

    private Dictionary<string, string>? _resolvedVersionProperties;

    public ArtifactProfile Profile => ArtifactProfile.Local;

    public DirectoryPath LocalFeedPath => _pathService.PackagesOutput;

    public async Task PrepareFeedAsync(BuildContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        cancellationToken.ThrowIfCancellationRequested();

        // SetupLocalDev local profile runs prerequisite pipeline before this resolver:
        // EnsureVcpkgDependencies -> Harvest -> ConsolidateHarvest.
        // Resolver then runs the versioned family packaging loop only.
        _cakeContext.EnsureDirectoryExists(_pathService.PackagesOutput);

        var concreteFamilies = ResolveConcreteFamilies();
        var timestampToken = DateTimeOffset.UtcNow.ToString("yyyyMMddTHHmmss", CultureInfo.InvariantCulture);

        var resolvedVersionProperties = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var family in concreteFamilies)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var version = BuildLocalVersion(family, timestampToken);

            await _packageTaskRunner.RunAsync(
                new PackageBuildConfiguration([family.Name], version),
                cancellationToken);

            var managedPackageId = FamilyIdentifierConventions.ManagedPackageId(family.Name);
            var nativePackageId = FamilyIdentifierConventions.NativePackageId(family.Name);

            EnsurePackageExists(managedPackageId, version);
            EnsurePackageExists(nativePackageId, version);

            resolvedVersionProperties[FamilyIdentifierConventions.VersionPropertyName(family.Name)] = version;
        }

        _resolvedVersionProperties = resolvedVersionProperties;
    }

    public async Task WriteConsumerOverrideAsync(BuildContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        cancellationToken.ThrowIfCancellationRequested();

        if (_resolvedVersionProperties is null || _resolvedVersionProperties.Count == 0)
        {
            throw new CakeException(
                "SetupLocalDev cannot write Janset.Smoke.local.props because no family versions were prepared. Run PrepareFeedAsync first.");
        }

        var propsFile = _pathService.GetSmokeLocalPropsFile();
        var directory = propsFile.GetDirectory();
        _cakeContext.EnsureDirectoryExists(directory);

        var xml = BuildSmokeLocalPropsContent(LocalFeedPath, _resolvedVersionProperties);
        await _cakeContext.WriteAllTextAsync(propsFile, xml);

        _log.Information("SetupLocalDev wrote local smoke override: {0}", propsFile.FullPath);
        _log.Information("SetupLocalDev local feed path: {0}", LocalFeedPath.FullPath);
    }

    private List<PackageFamilyConfig> ResolveConcreteFamilies()
    {
        var concreteFamilies = _manifestConfig.PackageFamilies
            .Where(family => !string.IsNullOrWhiteSpace(family.ManagedProject) && !string.IsNullOrWhiteSpace(family.NativeProject))
            .OrderBy(family => family.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (concreteFamilies.Count == 0)
        {
            throw new CakeException(
                "SetupLocalDev could not find any concrete package family (managed_project + native_project) in manifest package_families[].");
        }

        return concreteFamilies;
    }

    private string BuildLocalVersion(PackageFamilyConfig family, string timestampToken)
    {
        var library = _manifestConfig.LibraryManifests.SingleOrDefault(candidate =>
            string.Equals(candidate.Name, family.LibraryRef, StringComparison.OrdinalIgnoreCase));

        if (library is null)
        {
            throw new CakeException(
                $"SetupLocalDev cannot derive local family version for '{family.Name}' because library_ref '{family.LibraryRef}' was not found in manifest library_manifests[].");
        }

        if (!NuGetVersion.TryParse(library.VcpkgVersion, out var upstreamVersion))
        {
            throw new CakeException(
                $"SetupLocalDev cannot derive local family version for '{family.Name}' because upstream vcpkg_version '{library.VcpkgVersion}' is invalid.");
        }

        return $"{upstreamVersion.Major}.{upstreamVersion.Minor}.0-local.{timestampToken}";
    }

    private void EnsurePackageExists(string packageId, string version)
    {
        var packagePath = _pathService.GetPackageOutputFile(packageId, version);
        if (_cakeContext.FileExists(packagePath))
        {
            return;
        }

        throw new CakeException(
            $"SetupLocalDev expected package '{packagePath.GetFilename().FullPath}' in local feed '{_pathService.PackagesOutput.FullPath}', but it was not found.");
    }

    private static string BuildSmokeLocalPropsContent(DirectoryPath localFeedPath, IReadOnlyDictionary<string, string> familyVersionProperties)
    {
        var propertyGroup = new XElement("PropertyGroup",
            new XElement("LocalPackageFeed", localFeedPath.FullPath));

        foreach (var pair in familyVersionProperties.OrderBy(p => p.Key, StringComparer.OrdinalIgnoreCase))
        {
            propertyGroup.Add(new XElement(pair.Key, pair.Value));
        }

        var document = new XDocument(
            new XDeclaration("1.0", "utf-8", null),
            new XElement("Project", propertyGroup));

        var body = document.ToString();
        return string.Concat(document.Declaration?.ToString(), "\n", body);
    }
}
