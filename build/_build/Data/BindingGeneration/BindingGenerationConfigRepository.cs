using Build.Data.BindingGeneration.Models;
using Build.Data.Manifest;
using Build.Data.Manifest.Models;
using Build.Results;

namespace Build.Data.BindingGeneration;

public interface IBindingGenerationConfigRepository
{
    Result<BindingGenerationConfig, BindingGenerationConfigError> Load(string familyId);
    IReadOnlyList<string> EnumerateEnabledFamilies();
}

/// <summary>
/// Per-family binding-generation config resolver. Sits one layer above
/// <see cref="IManifestRepository"/> — the manifest is deserialized once by
/// <see cref="ManifestRepository"/> into the strongly-typed
/// <see cref="ManifestConfig"/> graph (manifest v2.2 requires
/// <see cref="LibraryManifest.BindingGeneration"/>), and this repo walks the typed
/// graph to map a family-id to its <see cref="BindingGenerationConfig"/>. No
/// hand-rolled JSON parsing — required-field validation flows through
/// <c>System.Text.Json</c>'s <c>required</c>-keyword machinery and surfaces as
/// <see cref="Cake.Core.CakeException"/> from the manifest layer.
/// <para>
/// The family-id ↔ library-name mapping is read from
/// <see cref="ManifestConfig.PackageFamilies"/> rather than hardcoded: each
/// <see cref="PackageFamilyConfig"/> declares its <see cref="PackageFamilyConfig.LibraryRef"/>
/// pointing at the matching <see cref="LibraryManifest.Name"/>. Manifest is the
/// single source of truth; adding a new family means adding a manifest entry,
/// not editing code.
/// </para>
/// </summary>
public sealed class BindingGenerationConfigRepository(IManifestRepository manifestRepository) : IBindingGenerationConfigRepository
{
    private readonly IManifestRepository _manifestRepository = manifestRepository ?? throw new ArgumentNullException(nameof(manifestRepository));

    public Result<BindingGenerationConfig, BindingGenerationConfigError> Load(string familyId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(familyId);

        var manifest = _manifestRepository.Load();

        var family = manifest.PackageFamilies.FirstOrDefault(pf => string.Equals(pf.Name, familyId, StringComparison.Ordinal));
        if (family is null)
        {
            return Result<BindingGenerationConfig, BindingGenerationConfigError>
                .Failure(BindingGenerationConfigError.FamilyNotFound(familyId));
        }

        var lib = manifest.LibraryManifests.FirstOrDefault(l => string.Equals(l.Name, family.LibraryRef, StringComparison.Ordinal));
        if (lib is null)
        {
            // Manifest is structurally inconsistent — package_families[].library_ref points
            // at a library that doesn't exist in library_manifests[]. PreFlight G49
            // (CoreLibraryIdentity) + related checks catch this normally; surface it here
            // as FamilyNotFound so the binding-generation caller gets an actionable error
            // rather than a NullReferenceException.
            return Result<BindingGenerationConfig, BindingGenerationConfigError>
                .Failure(BindingGenerationConfigError.FamilyNotFound(familyId));
        }

        if (!lib.BindingGeneration.Enabled)
        {
            return Result<BindingGenerationConfig, BindingGenerationConfigError>
                .Failure(BindingGenerationConfigError.Disabled(familyId));
        }

        return Result<BindingGenerationConfig, BindingGenerationConfigError>
            .Success(lib.BindingGeneration with { FamilyId = familyId });
    }

    public IReadOnlyList<string> EnumerateEnabledFamilies()
    {
        var manifest = _manifestRepository.Load();

        var familyIdByLibraryName = manifest.PackageFamilies
            .ToDictionary(pf => pf.LibraryRef, pf => pf.Name, StringComparer.Ordinal);

        return [.. manifest.LibraryManifests
            .Where(l => l.BindingGeneration.Enabled && familyIdByLibraryName.ContainsKey(l.Name))
            .Select(l => familyIdByLibraryName[l.Name]),];
    }
}
