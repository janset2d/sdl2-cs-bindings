using System.Text.Json;
using Build.Data.BindingGeneration;
using Build.Data.BindingGeneration.Models;
using Build.Host;
using Build.Host.Cake;
using Build.Results;
using Cake.Common.IO;
using Cake.Core;

namespace Build.Validation.BindingGeneration;

internal static class GeneratedStampValidator
{
    public static string Remediation(string family) =>
        $"run dotnet run --file tools.cs -- generate-bindings --execute --scope {family} ... and commit the generated files and the stamp.";

    public static async Task<ValidationReport> ValidateAsync(BuildContext context, IReadOnlyList<BindingGenerationConfig> families, bool requireHeaders = false)
    {
        var checks = new List<ValidationCheck>();
        var repository = new GeneratedStampRepository(context);
        var inputs = new GeneratedStampInputs(context, context.Paths);

        foreach (var family in families)
        {
            context.CancellationToken.ThrowIfCancellationRequested();
            var root = context.Paths.RepoRoot.Combine(family.GeneratedRoot);
            var stampPath = root.CombineWithFilePath(".generated-stamp");

            if (!context.FileExists(stampPath))
            {
                checks.Add(Error(family.FamilyId, "missing stamp"));
                continue;
            }

            try
            {
                var json = CakeJsonExtensions.DeserializeJson<JsonElement>(await context.ReadAllTextAsync(stampPath));

                if (json.ValueKind != JsonValueKind.Object || !json.TryGetProperty("schema_version", out var schema) ||
                    schema.ValueKind != JsonValueKind.Number || !schema.TryGetInt32(out var version))
                {
                    checks.Add(Error(family.FamilyId, "invalid stamp schema_version"));
                    continue;
                }

                if (version != 2)
                {
                    checks.Add(Error(family.FamilyId, "unknown schema_version: " + schema.GetRawText()));
                    continue;
                }

                var recorded = await repository.LoadAsync(root, context.CancellationToken);
                var generated = await inputs.ReadGeneratedAsync(root, context.CancellationToken);
                var current = await inputs.CaptureAsync(family, recorded.VcpkgTriplet, recorded.UsePlatformHeaderShims, generated, context.CancellationToken);
                current = await CheckHeadersAsync(context, inputs, family, current, checks, requireHeaders);
                Compare(context, recorded, current, checks);
            }
            catch (Exception error) when (error is JsonException or CakeException or IOException or InvalidOperationException or KeyNotFoundException)
            {
                checks.Add(Error(family.FamilyId, "invalid stamp or unavailable input: " + error.Message));
            }
        }

        return new ValidationReport(checks);
    }

    private static async Task<GeneratedStamp> CheckHeadersAsync(BuildContext context, GeneratedStampInputs inputs, BindingGenerationConfig family,
        GeneratedStamp current, List<ValidationCheck> checks, bool requireHeaders)
    {
        try
        {
            var headers = await inputs.CaptureHeadersAsync(current, family, context.CancellationToken);

            if (headers is not null)
            {
                return headers;
            }

            var message = $"{family.FamilyId}: header content not checked (header_inputs, header_fingerprint, header_input_count); " +
                $"recorded triplet {current.VcpkgTriplet} is not installed.";
            checks.Add(new ValidationCheck("headers", requireHeaders ? ValidationSeverity.Error : ValidationSeverity.Warning,
                requireHeaders ? message + " " + Remediation(family.FamilyId) : message));
        }
        catch (CakeException error)
        {
            checks.Add(Error(family.FamilyId, "header_inputs: " + error.Message));
        }

        return current;
    }

    private static void Compare(BuildContext context, GeneratedStamp recorded, GeneratedStamp current, List<ValidationCheck> checks)
    {
        var before = CakeJsonExtensions.DeserializeJson<JsonElement>(StampContent.Canonical(context, recorded));
        var after = CakeJsonExtensions.DeserializeJson<JsonElement>(StampContent.Canonical(context, current));

        foreach (var property in after.EnumerateObject())
        {
            if (current.HeaderFingerprint.Length == 0 && property.Name is "header_inputs" or "header_fingerprint" or "header_input_count")
            {
                continue;
            }

            var old = before.GetProperty(property.Name);

            if (property.Name == "generated_files")
            {
                CompareOutputs(recorded.Family, old, property.Value, checks);
            }
            else if (!JsonElement.DeepEquals(old, property.Value))
            {
                checks.Add(Error(current.Family, "stale " + property.Name));
            }
        }
    }

    private static void CompareOutputs(string family, JsonElement before, JsonElement after, List<ValidationCheck> checks)
    {
        var oldFiles = before.EnumerateObject().ToDictionary(property => property.Name, property => property.Value.GetString(), StringComparer.Ordinal);
        var newFiles = after.EnumerateObject().ToDictionary(property => property.Name, property => property.Value.GetString(), StringComparer.Ordinal);

        foreach (var path in oldFiles.Keys.Union(newFiles.Keys, StringComparer.Ordinal).Order(StringComparer.Ordinal))
        {
            var oldExists = oldFiles.TryGetValue(path, out var oldHash);
            var newExists = newFiles.TryGetValue(path, out var newHash);

            if (oldHash != newHash)
            {
                var kind = (oldExists, newExists) switch
                {
                    (false, _) => "extra",
                    (_, false) => "missing",
                    _ => "edited",
                };
                checks.Add(Error(family, $"generated_files: {kind} {path}"));
            }
        }
    }

    private static ValidationCheck Error(string family, string field) => new(field, ValidationSeverity.Error, $"{family}: {field}; {Remediation(family)}");
}
