using System.Globalization;
using Build.Data.BindingGeneration;
using Build.Data.BindingGeneration.Models;
using Build.Host;
using Build.Host.Cake;
using Build.Targets.GenerateBindings.HeaderSet;
using Build.Targets.GenerateBindings.Oracle;
using Build.Validation.BindingGeneration;
using Cake.Common.Diagnostics;
using Cake.Common.IO;
using Cake.Core;
using Cake.Core.IO;
using Cake.Frosting;

namespace Build.Targets.GenerateBindings;

[TaskName("GenerateBindings")]
[TaskDescription("Plans or generates binding families and optionally writes oracle evidence")]
public sealed class GenerateBindingsTask(
    IBindingGenerationConfigRepository configurations,
    IDynapiManifestRepository dynapiRepository,
    IEnumerable<IBindingFamilyValidator> validators) : AsyncFrostingTask<BuildContext>
{
    public override async Task RunAsync(BuildContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var requested = context.ResolveVersionsScope.ToHashSet(StringComparer.Ordinal);
        var enabled = configurations.EnumerateEnabledFamilies();
        var profiles = new Dictionary<string, BindingGenerationConfig>(StringComparer.Ordinal);

        IEnumerable<string> selection = requested.Count == 0 ? enabled : requested;

        foreach (var family in selection)
        {
            var result = configurations.Load(family);

            if (result.TryGetError(out var error))
            {
                throw new CakeException(error.Reason);
            }

            profiles.Add(family, result.Value);
        }

        var selected = enabled.Where(profiles.ContainsKey).Select(family => profiles[family]).ToArray();

        foreach (var family in selected)
        {
            ValidatorIdIntegrityCheck.AssertKnown(family, validators.Select(validator => validator.ValidatorId));
        }

        if (context.GenerateBindingsExecute || !context.GenerateBindingsEvidence)
        {
            var plan = new GenerationPlan(context, configurations.LoadGlobalSettings(), selected);
            var description = plan.Describe().ToList();

            foreach (var line in description)
            {
                context.Information("{0}", line);
            }

            if (context.GenerateBindingsExecute)
            {
                await GenerateAsync(context, plan, description);
            }

            if (!context.GenerateBindingsEvidence)
            {
                return;
            }
        }

        var collector = new OracleEvidenceCollector(context, context.Paths, dynapiRepository);
        var reportResult = await collector.BuildReportAsync(selected, context.CancellationToken);

        if (reportResult.TryGetError(out var resolutionError))
        {
            throw new CakeException(resolutionError.Reason);
        }

        var report = reportResult.Value;
        await collector.WriteReportAsync(report);

        foreach (var family in report.Families)
        {
            context.Information("Evidence family: {0}", family.Config.FamilyId);
            context.Information("Raw ABI findings: {0}; ownership warnings: {1}; required-surface findings: {2}",
                family.RawAbiChecks.Count, family.Ownership?.Count ?? 0, family.RequiredAudit?.Findings.Count ?? 0);
        }

        context.Information("Report: artifacts/binding-generation/oracle-evidence.md");
    }

    private async Task GenerateAsync(BuildContext context, GenerationPlan plan, List<string> diagnostics)
    {
        var resolver = new HeaderSetResolver(context);

        foreach (var family in plan.Families)
        {
            resolver.Resolve(family, context.Paths.GetVcpkgInstalledDir, plan.ShimRoot, context.Runtime.Triplet);

            if (family.FamilyId == "sdl2-core" && !context.FileExists(plan.IncludeDirectory(family).CombineWithFilePath("SDL.h")))
            {
                throw new CakeException($"Family '{family.FamilyId}' required header was not found at '{plan.IncludeDirectory(family).FullPath}/SDL.h'.");
            }
        }

        var staging = new DirectoryPath(System.IO.Path.Combine(System.IO.Path.GetTempPath(), "janset-binding-generation", Guid.NewGuid().ToString("N")));
        var clangSharp = new ClangSharpHeaderGeneration(context, diagnostics);

        try
        {
            clangSharp.GenerateNeutral(plan, staging);
            var requiredFailed = await GenerateRequiredSurfaceAsync(context, plan, staging, diagnostics);
            await clangSharp.GeneratePlatformViewsAsync(plan, staging);
            var postprocessFailed = await ApplyPostProcessAsync(context, plan, staging, diagnostics);
            var validationFailed = await ValidateStagedAsync(context, plan, staging, diagnostics);
            var exitCode = ClangSharpOutputClassifier.ExitCode(clangSharp.HasFailure, requiredFailed || postprocessFailed, clangSharp.HasEmptyOutput);

            if (exitCode == 0 && validationFailed)
            {
                exitCode = 1;
            }

            diagnostics.Add("Generation exit code: " + exitCode.ToString(CultureInfo.InvariantCulture));

            if (exitCode != 0)
            {
                throw new CakeException(exitCode, "Binding generation failed; see artifacts/binding-generation/generation-run.md.");
            }

            var snapshots = new List<GeneratedFamilySnapshot>();

            foreach (var family in plan.Families)
            {
                var root = GenerationPlan.StagedRoot(staging, family);
                var files = new List<GeneratedFile>();

                foreach (var file in context.FileSystem.GetDirectory(root).GetFiles("*", SearchScope.Recursive).Select(file => file.Path))
                {
                    files.Add(new GeneratedFile(root.GetRelativePath(file).FullPath, await context.ReadAllTextAsync(file)));
                }

                var destination = context.Paths.RepoRoot.Combine(family.GeneratedRoot);
                snapshots.Add(new GeneratedFamilySnapshot(destination.GetParent(), destination, files));
            }

            var backups = new DirectoryPath(staging.FullPath + "-backups");
            await new GeneratedFamilyPublisher(context).PublishAsync(snapshots, backups, context.CancellationToken);
        }
        finally
        {
            var reportPath = context.Paths.ArtifactsDir.Combine("binding-generation").CombineWithFilePath("generation-run.md");
            await context.WriteAllTextAsync(reportPath, string.Join('\n', diagnostics) + "\n");

            if (context.DirectoryExists(staging))
            {
                context.DeleteDirectory(staging, new DeleteDirectorySettings { Recursive = true, Force = true });
            }
        }
    }

    private static async Task<bool> GenerateRequiredSurfaceAsync(BuildContext context, GenerationPlan plan, DirectoryPath staging, List<string> diagnostics)
    {
        var failed = false;

        foreach (var family in plan.Families.Where(family => family.FamilyId == "sdl2-core"))
        {
            var source = await context.ReadAllTextAsync(plan.IncludeDirectory(family).CombineWithFilePath("SDL.h"));
            var rendered = RequiredSdlHeader.Render(source, family);

            if (rendered.TryGetError(out var error))
            {
                diagnostics.Add(error);
                failed = true;
                continue;
            }

            foreach (var codegen in GenerationPlan.Codegens)
            {
                await context.WriteAllTextAsync(GenerationPlan.StagedRoot(staging, family).Combine(codegen).CombineWithFilePath("SDL_required.g.cs"), rendered.Value);
            }
        }

        return failed;
    }

    private async Task<bool> ApplyPostProcessAsync(BuildContext context, GenerationPlan plan, DirectoryPath staging, List<string> diagnostics)
    {
        var config = new PostProcess.Config.FamilyConfig(configurations);
        var failed = false;

        foreach (var (mode, codegen, family) in BindingPostProcess.Invocations(plan.Families))
        {
            context.CancellationToken.ThrowIfCancellationRequested();
            diagnostics.Add($"Postprocess: {mode} {family.FamilyId} {codegen}; owner: {family.OwnerMode}; handles namespace: {family.ManagedNamespace}");

            try
            {
                await BindingPostProcess.ApplyAsync(context, config, family, GenerationPlan.StagedRoot(staging, family).Combine(codegen), mode);
            }
            catch (Exception error) when (error is not OperationCanceledException)
            {
                failed = true;
                diagnostics.Add($"Postprocess failure: {mode}: {error.Message}");
            }
        }

        return failed;
    }

    private async Task<bool> ValidateStagedAsync(BuildContext context, GenerationPlan plan, DirectoryPath staging, List<string> diagnostics)
    {
        var failed = false;

        foreach (var family in plan.Families)
        {
            var root = GenerationPlan.StagedRoot(staging, family);
            var source = await CSharpEvidenceLoader.LoadPathAsync(context, staging, staging.GetRelativePath(root).FullPath);

            foreach (var macro in source.Evidence.Constants.Where(constant => constant.NativeTypeName.StartsWith("#define ", StringComparison.Ordinal)))
            {
                diagnostics.Add($"Emitted macro: {family.FamilyId} {macro.Name} = {macro.Initializer}; {macro.SourcePath}");
            }

            foreach (var validator in validators.Where(validator => family.Validators.TryGetValue(validator.ValidatorId, out var enabled) && enabled))
            {
                try
                {
                    var entryPoints = source.Evidence.Functions
                        .Where(function => function.IsNativeImportDeclaration)
                        .Select(function => function.NativeEntryPoint)
                        .ToHashSet(StringComparer.Ordinal);
                    var report = await validator.ValidateAsync(entryPoints, family, context.CancellationToken);
                    diagnostics.Add(string.Create(CultureInfo.InvariantCulture,
                        $"Validator {validator.ValidatorId}: {family.FamilyId}; errors: {report.Errors.Count}; warnings: {report.Warnings.Count}"));
                    failed |= !report.IsValid;

                    foreach (var check in report)
                    {
                        diagnostics.Add($"{check.Severity}: {check.Message}");
                        context.Information("{0}: {1}", check.Severity, check.Message);
                    }
                }
                catch (CakeException error)
                {
                    failed = true;
                    diagnostics.Add($"Validator {validator.ValidatorId}: {family.FamilyId}: {error.Message}");
                }
            }
        }

        return failed;
    }

}
