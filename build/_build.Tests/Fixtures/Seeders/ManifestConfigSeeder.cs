using System.Collections.Immutable;
using System.Text.Json;
using Build.Context.Models;
using Build.Modules.Strategy.Models;

namespace Build.Tests.Fixtures.Seeders;

/// <summary>
/// Writes <c>build/manifest.json</c> from either a live <see cref="ManifestConfig"/> instance
/// or a canonical fixture JSON file shipped alongside the tests. Default behavior loads the
/// minimal fixture at <c>Fixtures/Data/manifest.fixture.json</c>; tests can replace fields
/// via the <c>With*</c> methods or supply a fully-built <see cref="ManifestConfig"/> via the
/// constructor overload.
/// <para>
/// Uses <see cref="JsonSerializerOptions.Default"/>-compatible serialization so the on-disk
/// representation round-trips through the production <c>ICakeContext.ToJson&lt;ManifestConfig&gt;</c>
/// reader.
/// </para>
/// </summary>
public sealed class ManifestConfigSeeder : IFixtureSeeder
{
    private static readonly JsonSerializerOptions WriteOptions = new()
    {
        WriteIndented = true,
    };

    private readonly ManifestConfig _manifest;

    public ManifestConfigSeeder(ManifestConfig manifest)
    {
        _manifest = manifest ?? throw new ArgumentNullException(nameof(manifest));
    }

    public ManifestConfig Manifest => _manifest;

    public void Apply(FakeRepoBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        var json = JsonSerializer.Serialize(_manifest, WriteOptions);
        builder.WithTextFile("build/manifest.json", json);
    }

    /// <summary>
    /// Load the canonical minimal fixture manifest from the test project's embedded data.
    /// The fixture is intentionally trimmed (one core library, one satellite, one runtime)
    /// — use <see cref="With*"/> to enrich, or construct a <see cref="ManifestConfig"/>
    /// directly for bespoke shapes.
    /// </summary>
    public static ManifestConfigSeeder FromDefaultFixture()
    {
        var manifest = LoadDefaultFixture();
        return new ManifestConfigSeeder(manifest);
    }

    public ManifestConfigSeeder WithLibraries(IEnumerable<LibraryManifest> libraries)
    {
        ArgumentNullException.ThrowIfNull(libraries);
        var replaced = _manifest with { LibraryManifests = libraries.ToImmutableList() };
        return new ManifestConfigSeeder(replaced);
    }

    public ManifestConfigSeeder WithRuntimes(IEnumerable<RuntimeInfo> runtimes)
    {
        ArgumentNullException.ThrowIfNull(runtimes);
        var replaced = _manifest with { Runtimes = runtimes.ToImmutableList() };
        return new ManifestConfigSeeder(replaced);
    }

    public ManifestConfigSeeder WithPackageFamilies(IEnumerable<PackageFamilyConfig> families)
    {
        ArgumentNullException.ThrowIfNull(families);
        var replaced = _manifest with { PackageFamilies = families.ToImmutableList() };
        return new ManifestConfigSeeder(replaced);
    }

    public ManifestConfigSeeder WithPackagingConfig(PackagingConfig packagingConfig)
    {
        ArgumentNullException.ThrowIfNull(packagingConfig);
        var replaced = _manifest with { PackagingConfig = packagingConfig };
        return new ManifestConfigSeeder(replaced);
    }

    public ManifestConfigSeeder WithCoreLibraryIdentityDrift(string packagingConfigCoreLibraryOverride)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(packagingConfigCoreLibraryOverride);
        var drifted = _manifest with
        {
            PackagingConfig = _manifest.PackagingConfig with { CoreLibrary = packagingConfigCoreLibraryOverride },
        };
        return new ManifestConfigSeeder(drifted);
    }

    private static ManifestConfig LoadDefaultFixture()
    {
        var path = LocateFixturePath();
        var json = File.ReadAllText(path);
        return JsonSerializer.Deserialize<ManifestConfig>(json)
            ?? throw new InvalidOperationException($"Failed to deserialize default manifest fixture from '{path}'.");
    }

    private static string LocateFixturePath()
    {
        var baseDir = AppContext.BaseDirectory;
        var candidate = Path.Combine(baseDir, "Fixtures", "Data", "manifest.fixture.json");
        if (File.Exists(candidate))
        {
            return candidate;
        }

        throw new FileNotFoundException(
            $"Default manifest fixture not found at '{candidate}'. Ensure 'Fixtures/Data/manifest.fixture.json' is copied to the test output directory via CopyToOutputDirectory.");
    }
}
