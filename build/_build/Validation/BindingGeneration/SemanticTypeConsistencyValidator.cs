using Build.Data.BindingGeneration.Models;
using Build.Results;
using Build.Targets.GenerateBindings.Model;

namespace Build.Validation.BindingGeneration;

/// <summary>
/// Fails generation when the semantic model still contains unresolved type-policy
/// outcomes. <see cref="NativeTypeKind.Unsupported"/> means the classifier could
/// not produce an emit-safe shape; <see cref="NativeTypeKind.Deferred"/> is only
/// allowed when the manifest explicitly records the declaration as a deliberate
/// stage boundary under <c>binding_generation.deferred_declarations</c>.
/// </summary>
public sealed class SemanticTypeConsistencyValidator : IBindingFamilyValidator
{
    private const string UnsupportedTypeCheckName = "Unsupported semantic type";
    private const string UnexpectedDeferredTypeCheckName = "Unexpected deferred semantic type";

    public string ValidatorId => "semantic-type-consistency";

    public Task<ValidationReport> ValidateAsync(BindingModel model, BindingGenerationConfig config, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(model);
        ArgumentNullException.ThrowIfNull(config);

        var configuredDeferredNames = config.DeferredDeclarations.Keys.ToHashSet(StringComparer.Ordinal);
        var checks = new List<ValidationCheck>();

        foreach (var occurrence in EnumerateTypeOccurrences(model))
        {
            ct.ThrowIfCancellationRequested();
            ValidateType(occurrence.Type, occurrence.Location, configuredDeferredNames, checks);
        }

        return Task.FromResult(checks.Count == 0 ? ValidationReport.Empty : new ValidationReport(checks));
    }

    private static IEnumerable<NativeTypeOccurrence> EnumerateTypeOccurrences(BindingModel model)
    {
        foreach (var view in model.Views)
        {
            foreach (var function in view.Functions)
            {
                yield return new NativeTypeOccurrence(function.ReturnType, $"{function.Name} return type");

                foreach (var parameter in function.Parameters)
                {
                    yield return new NativeTypeOccurrence(parameter.Type, $"{function.Name} parameter '{parameter.Name}'");
                }
            }
        }

        foreach (var structure in model.Structs)
        {
            foreach (var field in structure.Fields)
            {
                yield return new NativeTypeOccurrence(field.Type, $"{structure.Name}.{field.Name}");
            }
        }

        foreach (var enumeration in model.Enums)
        {
            yield return new NativeTypeOccurrence(enumeration.UnderlyingType, $"{enumeration.Name} underlying type");
        }

        foreach (var constant in model.Constants)
        {
            yield return new NativeTypeOccurrence(constant.Type, $"{constant.Name} constant type");
        }

        foreach (var handle in model.Handles)
        {
            yield return new NativeTypeOccurrence(handle.Type, $"{handle.Name} handle type");
        }

        foreach (var callback in model.Callbacks)
        {
            yield return new NativeTypeOccurrence(callback.ReturnType, $"{callback.Name} return type");

            foreach (var parameter in callback.Parameters)
            {
                yield return new NativeTypeOccurrence(parameter.Type, $"{callback.Name} parameter '{parameter.Name}'");
            }
        }
    }

    private static void ValidateType(
        NativeTypeRef type,
        string location,
        IReadOnlySet<string> configuredDeferredNames,
        List<ValidationCheck> checks)
    {
        if (type.Kind is NativeTypeKind.Unsupported)
        {
            checks.Add(new ValidationCheck(
                Name: UnsupportedTypeCheckName,
                Severity: ValidationSeverity.Error,
                Message: $"Semantic type '{type.NativeName}' at {location} is unsupported. Managed shape '{type.ManagedName}' would leak a classifier failure into generated bindings."));
        }
        else if (type.Kind is NativeTypeKind.Deferred && !IsConfiguredDeferred(type, configuredDeferredNames))
        {
            checks.Add(new ValidationCheck(
                Name: UnexpectedDeferredTypeCheckName,
                Severity: ValidationSeverity.Error,
                Message: $"Semantic type '{type.NativeName}' at {location} is deferred, but build/manifest.json binding_generation.deferred_declarations does not list it."));
        }

        if (type.ElementType is not null)
        {
            ValidateType(type.ElementType, $"{location} element type", configuredDeferredNames, checks);
        }
    }

    private static bool IsConfiguredDeferred(NativeTypeRef type, IReadOnlySet<string> configuredDeferredNames) =>
        configuredDeferredNames.Contains(type.NativeName) || configuredDeferredNames.Contains(type.ManagedName);

    private sealed record NativeTypeOccurrence(NativeTypeRef Type, string Location);
}
