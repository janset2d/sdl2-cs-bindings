using Build.Data.BindingGeneration;
using Build.Data.BindingGeneration.Models;
using Build.Host;
using Build.Host.Cake;
using Build.Results;
using Build.Targets.GenerateBindings.Emit;
using Build.Targets.GenerateBindings.HeaderSet;
using Build.Targets.GenerateBindings.Model;
using Build.Targets.GenerateBindings.ModelBuilding;
using Build.Targets.GenerateBindings.ModelBuilding.Types;
using Build.Targets.GenerateBindings.Parse;
using Build.Targets.GenerateBindings.PlatformViews;
using Build.Validation.BindingGeneration;
using Cake.Common.IO;
using Cake.Core;
using Cake.Core.Diagnostics;
using Cake.Core.IO;

namespace Build.Targets.GenerateBindings;

public sealed class BindingFamilyGeneration(
    IBindingGenerationConfigRepository configRepository,
    HeaderSetResolver headerResolver,
    ICppAstParseRunner parseRunner,
    BindingModelBuilder modelBuilder,
    BindingEmitter emitter,
    IEnumerable<IBindingFamilyValidator> validators,
    ICakeLog log)
{
    private readonly IBindingGenerationConfigRepository _configRepository = configRepository ?? throw new ArgumentNullException(nameof(configRepository));
    private readonly HeaderSetResolver _headerResolver = headerResolver ?? throw new ArgumentNullException(nameof(headerResolver));
    private readonly ICppAstParseRunner _parseRunner = parseRunner ?? throw new ArgumentNullException(nameof(parseRunner));
    private readonly BindingModelBuilder _modelBuilder = modelBuilder ?? throw new ArgumentNullException(nameof(modelBuilder));
    private readonly BindingEmitter _emitter = emitter ?? throw new ArgumentNullException(nameof(emitter));
    private readonly IReadOnlyList<IBindingFamilyValidator> _validators = validators?.ToList() ?? throw new ArgumentNullException(nameof(validators));
    private readonly ICakeLog _log = log ?? throw new ArgumentNullException(nameof(log));

    public async Task GenerateAsync(BuildContext context, string familyId, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentException.ThrowIfNullOrWhiteSpace(familyId);

        var configResult = _configRepository.Load(familyId);
        if (configResult.TryGetError(out var error))
        {
            throw new CakeException($"Family '{familyId}' config load failed: {error.Reason}");
        }
        var config = configResult.Value;

        var triplet = context.Runtime.Triplet;
        var vcpkgInstalledRoot = context.Paths.GetVcpkgInstalledDir;
        var syntheticHeadersRoot = context.Paths.BindingGeneratorSyntheticHeadersRoot;
        var outputDirectory = context.Paths.GetGenerateBindingsPreviewFamilyRoot(config.FamilyId);

        _log.Information(
            "Generating '{0}' bindings to '{1}' (namespace {2}, primary class {3}, triplet '{4}').",
            config.FamilyId, outputDirectory.FullPath, config.ManagedNamespace, config.PrimaryClassName, triplet);

        var headerSet = _headerResolver.Resolve(config, vcpkgInstalledRoot, syntheticHeadersRoot, triplet);

        _log.Information("Resolved {0} headers under '{1}'.", headerSet.Headers.Count, headerSet.FamilyIncludeDirectory.FullPath);

        var catalog = PlatformCatalog.For(config.PlatformCatalogId);

        // Pre-announce the parse views BEFORE entering the parallel loop. Cake
        // ICakeLog is not documented thread-safe, so calling _log.Information(...)
        // from inside the PLINQ Select(...) lambda would risk interleaved or
        // corrupted log output across the concurrent view tasks. Logging the
        // catalog up front is harmless: the per-view function counts that
        // matter for diagnostics are emitted by LogPerViewCounts(model) after
        // the merge, sequentially.
        foreach (var view in catalog.ParseViews)
        {
            _log.Information("Parsing view '{0}' ({1} defines, {2} undefines).", view.Name, view.Defines.Count, view.Undefines.Count);
        }

        // Outer per-view loop runs in parallel — each CppParser.ParseFile call
        // constructs its own libclang translation unit with no shared mutable
        // state (verified against the CppAst.NET source — ParseInternal calls
        // CXIndex.Create() per invocation), so view-level parallelism is safe.
        // AsOrdered() preserves catalog order in the output even when view
        // tasks complete out-of-order, so the translator + emitter see a
        // deterministic input sequence regardless of host CPU count. Inner
        // per-header loop inside CppAstParseRunner stays sequential
        // (libclang TU resource recycling within a view).
        var parseResults = catalog.ParseViews
            .AsParallel()
            .AsOrdered()
            .WithCancellation(ct)
            .Select(view => _parseRunner.Parse(config, headerSet, view))
            .ToList();

        var model = _modelBuilder.Build(parseResults, config, ConvertRequiredFunctions(config));
        LogPerViewCounts(model);

        await RunFamilyValidatorsAsync(model, config, ct).ConfigureAwait(false);

        var fileSet = _emitter.Emit(model, BindingEmissionOptions.FromConfig(config));
        await WriteAsync(context, fileSet, outputDirectory, ct).ConfigureAwait(false);

        _log.Information("Wrote {0} files to '{1}'.", fileSet.Files.Count, outputDirectory.FullPath);
    }

    /// <summary>
    /// Maps <see cref="BindingGenerationConfig.RequiredFunctions"/> (config record shape
    /// with string-typed return + parameter types) to the semantic
    /// <see cref="BindingFunction"/> shape. This remains a narrow config adapter:
    /// parsed declarations flow through <see cref="NativeTypeClassifier"/>, while
    /// manifest-required functions have no CppAst node to classify.
    /// </summary>
    private static IReadOnlyList<BindingFunction> ConvertRequiredFunctions(BindingGenerationConfig config)
    {
        return [.. config.RequiredFunctions.Select(rf =>
            new BindingFunction(
                Name: rf.Name,
                ReturnType: LegacyBindingTypeRefBridge.ToNative(BindingTypeRef.Of(rf.ReturnType)),
                Parameters: [.. rf.Parameters.Select(p => new BindingParameter(LegacyBindingTypeRefBridge.ToNative(BindingTypeRef.Of(p.Type)), p.Name))],
                SourceHeader: rf.SourceHeader))];
    }

    private async Task RunFamilyValidatorsAsync(BindingModel model, BindingGenerationConfig config, CancellationToken ct)
    {
        var enabledIds = config.Validators
            .Where(kv => kv.Value)
            .Select(kv => kv.Key)
            .ToHashSet(StringComparer.Ordinal);

        var registeredIds = _validators
            .Select(v => v.ValidatorId)
            .ToHashSet(StringComparer.Ordinal);

        var unknownIds = enabledIds
            .Where(id => !registeredIds.Contains(id))
            .Order(StringComparer.Ordinal)
            .ToArray();
        if (unknownIds.Length > 0)
        {
            throw new CakeException(
                $"GenerateBindings family '{config.FamilyId}' enables unknown validator id(s): {string.Join(", ", unknownIds)}. " +
                "Register the validator or fix build/manifest.json library_manifests[].binding_generation.validators.");
        }

        var toRun = _validators.Where(v => enabledIds.Contains(v.ValidatorId)).ToList();

        foreach (var validator in toRun)
        {
            var report = await validator.ValidateAsync(model, config, ct).ConfigureAwait(false);
            LogReport(report, validator.ValidatorId);
            if (!report.IsValid)
            {
                throw new CakeException(
                    $"Validator '{validator.ValidatorId}' failed for family '{config.FamilyId}': {report.Errors.Count} error(s). See preceding log lines.");
            }
        }
    }

    private void LogReport(ValidationReport report, string validatorId)
    {
        foreach (var warning in report.Warnings)
        {
            _log.Warning("[{0}] {1}: {2}", validatorId, warning.Name, warning.Message);
        }
        foreach (var error in report.Errors)
        {
            _log.Error("[{0}] {1}: {2}", validatorId, error.Name, error.Message);
        }
    }

    private void LogPerViewCounts(BindingModel model)
    {
        _log.Information(
            "Model categories: {0} structs, {1} enums, {2} constants, {3} handles, {4} callbacks.",
            model.Structs.Count,
            model.Enums.Count,
            model.Constants.Count,
            model.Handles.Count,
            model.Callbacks.Count);

        foreach (var view in model.Views)
        {
            _log.Information("View {0}: {1} functions.", view.Name, view.Functions.Count);
        }
    }

    private static async Task WriteAsync(BuildContext context, GeneratedFileSet fileSet, DirectoryPath outputDirectory, CancellationToken ct)
    {
        EnsureSafeGeneratedOutputDirectory(context.Environment, context.Paths.GenerateBindingsPreviewRoot, outputDirectory);

        if (context.DirectoryExists(outputDirectory))
        {
            ClearDirectoryContents(context, outputDirectory);
        }

        context.EnsureDirectoryExists(outputDirectory);

        foreach (var file in fileSet.Files)
        {
            ct.ThrowIfCancellationRequested();

            var targetPath = outputDirectory.CombineWithFilePath(file.RelativePath);
            var parent = targetPath.GetDirectory();
            context.EnsureDirectoryExists(parent);

            await context.WriteAllTextAsync(targetPath, file.Content).ConfigureAwait(false);
        }
    }

    private static void ClearDirectoryContents(BuildContext context, DirectoryPath outputDirectory)
    {
        var files = context.GetFiles($"{outputDirectory.FullPath}/**/*")
            .Concat(context.GetFiles($"{outputDirectory.FullPath}/*"))
            .DistinctBy(file => file.FullPath);
        foreach (var file in files)
        {
            context.DeleteFile(file);
        }

        var directories = context.GetDirectories($"{outputDirectory.FullPath}/**/*")
            .Concat(context.GetDirectories($"{outputDirectory.FullPath}/*"))
            .DistinctBy(directory => directory.FullPath)
            .OrderByDescending(directory => directory.Segments.Length)
            .ThenByDescending(directory => directory.FullPath, StringComparer.Ordinal);
        foreach (var directory in directories)
        {
            context.DeleteDirectory(directory, new DeleteDirectorySettings
            {
                Recursive = true,
                Force = true,
            });
        }
    }

    private static void EnsureSafeGeneratedOutputDirectory(ICakeEnvironment environment, DirectoryPath previewRoot, DirectoryPath outputDirectory)
    {
        var root = previewRoot.MakeAbsolute(environment);
        var candidate = outputDirectory.MakeAbsolute(environment);
        var rootSegments = root.Segments;
        var candidateSegments = candidate.Segments;

        if (candidateSegments.Length <= rootSegments.Length)
        {
            throw new CakeException($"Refusing to clear generated bindings output outside '{root.FullPath}': '{candidate.FullPath}'.");
        }

        for (var i = 0; i < rootSegments.Length; i++)
        {
            if (!string.Equals(rootSegments[i], candidateSegments[i], StringComparison.OrdinalIgnoreCase))
            {
                throw new CakeException($"Refusing to clear generated bindings output outside '{root.FullPath}': '{candidate.FullPath}'.");
            }
        }
    }
}
