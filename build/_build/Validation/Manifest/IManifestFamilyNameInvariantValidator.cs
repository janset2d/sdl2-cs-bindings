using Build.Data.Manifest.Models;
using Build.Results;

namespace Build.Validation.Manifest;
/// <summary>
/// Validates that every <c>package_families[].name</c> is lowercase kebab-case
/// matching the canonical <c>sdl&lt;major&gt;-&lt;role&gt;</c> pattern. Hand-edited
/// mixed-case entries (for example, <c>SDL2-Core</c>) silently bypass
/// <c>PackageFamilyId</c> ordinal-exact lookups in downstream consumers; this
/// validator catches the drift at PreFlight time before any build operation runs.
/// PreFlight guardrail G59.
/// </summary>
public interface IManifestFamilyNameInvariantValidator
{
    public ValidationReport Validate(ManifestConfig manifest);
}
