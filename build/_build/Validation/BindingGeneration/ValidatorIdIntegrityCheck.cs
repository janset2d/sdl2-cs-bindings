using Build.Data.BindingGeneration.Models;
using Cake.Core;

namespace Build.Validation.BindingGeneration;

public static class ValidatorIdIntegrityCheck
{
    public static void AssertKnown(BindingGenerationConfig config, IEnumerable<string> registeredIds)
    {
        ArgumentNullException.ThrowIfNull(config);
        var registered = registeredIds.ToHashSet(StringComparer.Ordinal);
        var unknownIds = config.Validators
            .Where(kv => kv.Value)
            .Select(kv => kv.Key)
            .Distinct(StringComparer.Ordinal)
            .Where(id => !registered.Contains(id))
            .Order(StringComparer.Ordinal)
            .ToArray();

        if (unknownIds.Length > 0)
        {
            throw new CakeException(
                $"Binding generation family '{config.FamilyId}' enables unknown validator id(s): {string.Join(", ", unknownIds)}. " +
                "Register the validator or fix build/manifest.json library_manifests[].binding_generation.validators.");
        }
    }
}
