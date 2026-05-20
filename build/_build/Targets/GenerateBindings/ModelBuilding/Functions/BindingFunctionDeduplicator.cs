using Build.Targets.GenerateBindings.Model;

namespace Build.Targets.GenerateBindings.ModelBuilding.Functions;

internal sealed class BindingFunctionDeduplicator
{
    private readonly Dictionary<string, BindingFunction> _emitted = new(StringComparer.Ordinal);

    public void AddRange(IEnumerable<BindingFunction> functions)
    {
        ArgumentNullException.ThrowIfNull(functions);

        foreach (var function in functions)
        {
            var key = CreateCSharpSignatureKey(function);
            if (_emitted.TryGetValue(key, out var existing))
            {
                EnsureCompatible(existing, function);
                continue;
            }

            _emitted.Add(key, function);
        }
    }

    public List<BindingFunction> ExcludeAlreadyEmitted(IEnumerable<BindingFunction> functions)
    {
        ArgumentNullException.ThrowIfNull(functions);

        var deduplicated = new List<BindingFunction>();
        foreach (var function in functions)
        {
            var key = CreateCSharpSignatureKey(function);
            if (_emitted.TryGetValue(key, out var existing))
            {
                EnsureCompatible(existing, function);
                continue;
            }

            _emitted.Add(key, function);
            deduplicated.Add(function);
        }

        return deduplicated;
    }

    private static string CreateCSharpSignatureKey(BindingFunction function) =>
        function.Name + "(" + string.Join(',', function.Parameters.Select(parameter => parameter.Type.ManagedName)) + ")";

    private static void EnsureCompatible(BindingFunction existing, BindingFunction candidate)
    {
        if (!string.Equals(existing.ReturnType.ManagedName, candidate.ReturnType.ManagedName, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"Platform parse views produced incompatible return types for '{candidate.Name}': " +
                $"'{existing.ReturnType.ManagedName}' vs '{candidate.ReturnType.ManagedName}'.");
        }
    }
}
