using Build.Data.BindingGeneration.Models;
using Build.Targets.GenerateBindings.Model;

namespace Build.Targets.GenerateBindings.Translation;

internal static class RequiredConstantTranslator
{
    public static IReadOnlyList<BindingConstant> Translate(IReadOnlyList<RequiredConstantConfig> requiredConstants)
    {
        ArgumentNullException.ThrowIfNull(requiredConstants);

        return [.. requiredConstants.Select(required => new BindingConstant(
            required.Name,
            BindingTypeRef.Of(required.Type),
            required.Value,
            required.Kind))];
    }
}
