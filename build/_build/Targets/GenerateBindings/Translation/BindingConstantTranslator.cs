using System.Globalization;
using Build.Data.BindingGeneration.Models;
using Build.Targets.GenerateBindings.Model;
using Build.Targets.GenerateBindings.Parsing;

namespace Build.Targets.GenerateBindings.Translation;

internal sealed record BindingConstantTranslationResult(
    IReadOnlyList<BindingConstant> Constants,
    MacroConstantReport Report);

internal sealed record MacroApiPolicyTranslationInputs(
    IReadOnlyList<MacroConstantCandidate> CandidateMacros,
    IReadOnlyList<MacroConstantCandidate> HelperCandidates,
    IReadOnlyList<MacroConstantReportEntry> PolicyEntries);

internal sealed record MacroHelperFunctionCoalesceResult(
    Dictionary<string, MacroFunctionLikeMacro> HelperFunctions,
    int DuplicateCoalescedCount);

/// <summary>
/// Orchestrates the source-first macro constant pipeline:
/// raw candidates from parsed headers → API-policy filter →
/// value classification → duplicate merge → manual policy overlay.
/// Required constants from <see cref="BindingGenerationConfig.RequiredConstants"/>
/// are injected by <see cref="MacroManualPolicyApplier"/> for headers excluded from
/// the per-header parse loop (e.g. SDL.h).
/// </summary>
internal static class BindingConstantTranslator
{
    public static BindingConstantTranslationResult Translate(
        IReadOnlyList<CppAstParseResult> parseResults,
        BindingGenerationConfig config)
    {
        ArgumentNullException.ThrowIfNull(parseResults);
        ArgumentNullException.ThrowIfNull(config);

        var rawCandidates = MacroCandidateCollector.Collect(parseResults);
        var parsedCount = rawCandidates.Count;

        var policyInputs = ApplyApiPolicy(rawCandidates);
        var helperFunctionResult = CoalesceHelperFunctions(policyInputs.HelperCandidates);
        var valueClassifications = ClassifyCandidateMacros(
            policyInputs.CandidateMacros,
            helperFunctionResult.HelperFunctions);

        var candidateCount = policyInputs.CandidateMacros.Count;
        var mergeResult = MacroConstantMerger.Merge(valueClassifications);
        var manualResult = MacroManualPolicyApplier.Apply(mergeResult.Constants, mergeResult.Entries, config);

        var allEntries = policyInputs.PolicyEntries
            .Concat(manualResult.Entries)
            .OrderBy(e => e.Name, StringComparer.Ordinal)
            .ThenBy(e => e.ParseViewName, StringComparer.Ordinal)
            .ThenBy(e => e.Disposition, StringComparer.Ordinal)
            .ThenBy(e => e.SourceHeader, StringComparer.Ordinal)
            .ToList();

        var report = new MacroConstantReport(
            ParsedCount: parsedCount,
            CandidateCount: candidateCount,
            EmittedCount: manualResult.Constants.Count,
            SkippedCount: allEntries.Count(e => e.Disposition == "skipped"),
            ExcludedCount: manualResult.ExcludedCount,
            OverriddenCount: manualResult.OverriddenCount,
            DuplicateCoalescedCount: mergeResult.DuplicateCoalescedCount,
            HelperCandidateCount: allEntries.Count(e => e.Disposition == "helper-candidate"),
            HelperDuplicateCoalescedCount: helperFunctionResult.DuplicateCoalescedCount,
            UnsupportedCount: allEntries.Count(e => e.Disposition == "unsupported"),
            ConflictCount: 0,
            Entries: allEntries);

        return new BindingConstantTranslationResult(manualResult.Constants, report);
    }

    private static MacroApiPolicyTranslationInputs ApplyApiPolicy(IReadOnlyList<MacroConstantCandidate> rawCandidates)
    {
        var policyEntries = new List<MacroConstantReportEntry>();
        var candidateMacros = new List<MacroConstantCandidate>();
        var helperCandidates = new List<MacroConstantCandidate>();

        foreach (var candidate in rawCandidates)
        {
            var decision = MacroApiPolicy.Classify(candidate);
            switch (decision.Disposition)
            {
                case MacroApiDisposition.Candidate:
                    candidateMacros.Add(candidate);
                    break;
                case MacroApiDisposition.HelperCandidate:
                    helperCandidates.Add(candidate);
                    policyEntries.Add(new MacroConstantReportEntry(
                        candidate.Name, candidate.SourceHeader, candidate.ParseViewName,
                        "helper-candidate", decision.Reason,
                        null, null,
                        MacroForm(candidate), "public-helper-candidate", candidate.Value, null));
                    break;
                case MacroApiDisposition.Unsupported:
                    policyEntries.Add(new MacroConstantReportEntry(
                        candidate.Name, candidate.SourceHeader, candidate.ParseViewName,
                        "unsupported", decision.Reason,
                        null, null,
                        MacroForm(candidate), "unsupported", candidate.Value, null));
                    break;
                case MacroApiDisposition.NonApi:
                    policyEntries.Add(new MacroConstantReportEntry(
                        candidate.Name, candidate.SourceHeader, candidate.ParseViewName,
                        "skipped", decision.Reason,
                        null, null,
                        MacroForm(candidate), "non-api", candidate.Value, null));
                    break;
            }
        }

        return new MacroApiPolicyTranslationInputs(candidateMacros, helperCandidates, policyEntries);
    }

    private static List<MacroValueClassification> ClassifyCandidateMacros(
        IReadOnlyList<MacroConstantCandidate> candidateMacros,
        Dictionary<string, MacroFunctionLikeMacro> helperFunctions)
    {
        var classifications = new List<MacroValueClassification>();
        var knownConstants = new Dictionary<string, MacroIntegerExpressionValue>(StringComparer.Ordinal);
        var pendingCandidates = new List<MacroConstantCandidate>(candidateMacros);

        while (pendingCandidates.Count > 0)
        {
            var progress = false;
            var nextPendingCandidates = new List<MacroConstantCandidate>();
            var context = new MacroExpressionEvaluationContext(knownConstants, helperFunctions);

            foreach (var candidate in pendingCandidates)
            {
                var classification = MacroValueClassifier.Classify(candidate, context);
                if (classification.Constant is null)
                {
                    nextPendingCandidates.Add(candidate);
                    continue;
                }

                classifications.Add(classification);
                progress = true;
                if (TryConvertConstantToKnownInteger(classification.Constant, out var knownInteger))
                    knownConstants.TryAdd(classification.Constant.Name, knownInteger);
            }

            if (!progress)
            {
                var finalContext = new MacroExpressionEvaluationContext(knownConstants, helperFunctions);
                foreach (var candidate in nextPendingCandidates)
                    classifications.Add(MacroValueClassifier.Classify(candidate, finalContext));

                break;
            }

            pendingCandidates = nextPendingCandidates;
        }

        return classifications;
    }

    private static MacroHelperFunctionCoalesceResult CoalesceHelperFunctions(
        IReadOnlyList<MacroConstantCandidate> helperCandidates)
    {
        var helperFunctions = new Dictionary<string, MacroFunctionLikeMacro>(StringComparer.Ordinal);
        var duplicateCoalescedCount = 0;
        foreach (var candidate in helperCandidates)
        {
            var function = new MacroFunctionLikeMacro(candidate.Name, candidate.Parameters, candidate.Value);
            if (!helperFunctions.TryGetValue(candidate.Name, out var existing))
            {
                helperFunctions.Add(candidate.Name, function);
                continue;
            }

            if (!existing.Parameters.SequenceEqual(function.Parameters, StringComparer.Ordinal)
                || !string.Equals(existing.Body, function.Body, StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    $"Incompatible helper macro definitions for '{candidate.Name}'.");
            }

            duplicateCoalescedCount++;
        }

        return new MacroHelperFunctionCoalesceResult(helperFunctions, duplicateCoalescedCount);
    }

    private static string MacroForm(MacroConstantCandidate candidate) =>
        candidate.IsFunctionLike ? "function-like" : "object-like";

    private static bool TryConvertConstantToKnownInteger(
        BindingConstant constant,
        out MacroIntegerExpressionValue value)
    {
        var text = constant.Value.Trim();
        var isUnsigned = constant.Type.ManagedName is "uint" or "ulong"
            || text.EndsWith('u')
            || text.EndsWith('U');
        var digits = text.TrimEnd('u', 'U', 'l', 'L');
        var isHex = digits.StartsWith("0x", StringComparison.OrdinalIgnoreCase);
        var parseText = isHex ? digits[2..] : digits;
        var style = isHex ? NumberStyles.HexNumber : NumberStyles.None;

        if (!ulong.TryParse(parseText, style, CultureInfo.InvariantCulture, out var parsed))
        {
            value = new MacroIntegerExpressionValue(0, false, null);
            return false;
        }

        value = new MacroIntegerExpressionValue(parsed, isUnsigned, constant.Kind == ConstantKind.Literal ? constant.Value : null);
        return true;
    }
}
