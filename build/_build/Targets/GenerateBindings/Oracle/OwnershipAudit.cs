using Build.Data.BindingGeneration.Models;
using Build.Results;

namespace Build.Targets.GenerateBindings.Oracle;

internal static class OwnershipAudit
{
    public static ValidationReport Run(CSharpEvidence evidence, BindingGenerationConfig config)
    {
        ArgumentNullException.ThrowIfNull(evidence);
        ArgumentNullException.ThrowIfNull(config);
        var imports = evidence.Functions.Where(function => function.IsNativeImportDeclaration).Select(function => function.NativeEntryPoint);
        var types = evidence.Types
            .Where(type => type.ContainingTypeAccessibilities.Count == 0 && type.Name != config.RawClassName && type.Name != config.PrimaryClassName)
            .Select(type => type.Name);
        return new ValidationReport(imports
            .Concat(types)
            .Distinct(StringComparer.Ordinal)
            .Where(symbol => !config.OwnedPrefixes.Any(prefix => symbol.StartsWith(prefix, StringComparison.Ordinal)))
            .Order(StringComparer.Ordinal)
            .Select(symbol => new ValidationCheck(symbol, ValidationSeverity.Warning, "not covered by owned_prefixes")));
    }
}
