using System.Globalization;
using Build.Host;
using Build.Host.Cake;
using Build.Tools.ClangSharp;
using Cake.Common.IO;
using Cake.Core;
using Cake.Core.IO;

namespace Build.Targets.GenerateBindings;

internal sealed class ClangSharpHeaderGeneration(BuildContext context, List<string> diagnostics)
{
    public bool HasFailure { get; private set; }
    public bool HasEmptyOutput { get; private set; }

    public void GenerateNeutral(GenerationPlan plan, DirectoryPath staging)
    {
        foreach (var codegen in GenerationPlan.Codegens)
        {
            foreach (var family in plan.Families)
            {
                foreach (var header in GenerationPlan.Headers(family))
                {
                    Generate(plan.Invocation(family, codegen, header, GenerationPlan.StagedRoot(staging, family)));
                }
            }
        }
    }

    public async Task GeneratePlatformViewsAsync(GenerationPlan plan, DirectoryPath staging)
    {
        foreach (var codegen in GenerationPlan.Codegens)
        {
            foreach (var family in plan.Families)
            {
                foreach (var header in family.PlatformSensitiveHeaders.Where(header => GenerationPlan.Headers(family).Contains(header, StringComparer.Ordinal)))
                {
                    var root = GenerationPlan.StagedRoot(staging, family);
                    var neutral = plan.Invocation(family, codegen, header, root).Output;
                    var exclusions = context.FileExists(neutral) ? NeutralSymbolExclusions.Extract(await context.ReadAllTextAsync(neutral)) : [];

                    foreach (var view in plan.Settings.PlatformViews)
                    {
                        Generate(plan.Invocation(family, codegen, header, root, view, exclusions));
                    }
                }
            }
        }
    }

    private void Generate(ClangSharpSettings invocation)
    {
        context.CancellationToken.ThrowIfCancellationRequested();
        context.EnsureDirectoryExists(invocation.Output.GetDirectory());
        ClangSharpResult result;

        try
        {
            result = ClangSharpTool.Run(new ClangSharpInvocationCapture(context, diagnostics), invocation);
        }
        catch (Exception error) when (error is CakeException or IOException)
        {
            HasFailure = true;
            diagnostics.Add($"ClangSharp process failure: {invocation.File.FullPath}: {error.Message}");
            return;
        }

        diagnostics.Add(string.Create(CultureInfo.InvariantCulture, $"Exit code: {result.ExitCode}; output: {invocation.Output.FullPath}"));
        var diagnosticOutput = string.Join('\n', result.StandardOutput.Concat(result.StandardError));
        diagnostics.Add(diagnosticOutput);
        var file = context.FileSystem.GetFile(invocation.Output);
        var output = ClangSharpOutputClassifier.Classify(result.ExitCode, file.Exists, file.Exists ? file.Length : 0, invocation.File.FullPath, diagnosticOutput);
        HasFailure |= output.Failed;
        HasEmptyOutput |= output.Empty;
        diagnostics.Add($"Accepted: {output.Accepted}; no-op: {output.NoOp}; empty: {output.Empty}; failure: {output.Failed}");
        diagnostics.Add("Skipped function-like macros: " + string.Join(", ", output.Macros));
        diagnostics.AddRange(output.Diagnostics);
    }
}
