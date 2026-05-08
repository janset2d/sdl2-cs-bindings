using Build.Results;
using Build.Shared.Manifest;

namespace Build.Validation.Manifest;

/// <summary>
/// Validates that every <c>package_families[].name</c> follows the canonical
/// <c>sdl&lt;major&gt;-&lt;role&gt;</c> kebab-case pattern. PreFlight guardrail G59.
/// </summary>
public interface IManifestFamilyNameInvariantValidator
{
    ValidationReport Validate(ManifestConfig manifest);
}
