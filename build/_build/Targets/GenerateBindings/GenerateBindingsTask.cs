using Build.Data.BindingGeneration;
using Build.Data.BindingGeneration.Models;
using Build.Host;
using Build.Host.Cake;
using Build.Results;
using Build.Targets.GenerateBindings.Emitting;
using Build.Targets.GenerateBindings.HeaderSet;
using Build.Targets.GenerateBindings.Model;
using Build.Targets.GenerateBindings.Parsing;
using Build.Targets.GenerateBindings.Translation;
using Build.Validation.BindingGeneration;
using Cake.Common.IO;
using Cake.Core;
using Cake.Core.Diagnostics;
using Cake.Core.IO;
using Cake.Frosting;

namespace Build.Targets.GenerateBindings;

// EnsureVcpkgDependencies must run before GenerateBindings. Cake IsDependentOn
// attributes are not used in this project; sequential dispatch is the caller's
// responsibility (docker/binding-generator-entrypoint.sh runs the two targets
// back to back inside the container, matching the tools.cs setup / ci-sim
// pattern that drives Cake host-side too).
[TaskName("GenerateBindings")]
[TaskDescription("Regenerates SDL2 family bindings driven by manifest.library_manifests[].binding_generation. Default: every family with binding_generation.enabled=true. Linux-canonical, runs inside linux-builder container.")]
public sealed class GenerateBindingsTask(
    IBindingGenerationConfigRepository configRepository,
    HeaderSetResolver headerResolver,
    ICppAstParseRunner parseRunner,
    ILibclangVersionAsserter libclangVersionAsserter,
    IEnumerable<IBindingFamilyValidator> validators,
    ICakeLog log) : AsyncFrostingTask<BuildContext>
{
    private readonly IBindingGenerationConfigRepository _configRepository = configRepository ?? throw new ArgumentNullException(nameof(configRepository));
    private readonly HeaderSetResolver _headerResolver = headerResolver ?? throw new ArgumentNullException(nameof(headerResolver));
    private readonly ICppAstParseRunner _parseRunner = parseRunner ?? throw new ArgumentNullException(nameof(parseRunner));
    private readonly ILibclangVersionAsserter _libclangVersionAsserter = libclangVersionAsserter ?? throw new ArgumentNullException(nameof(libclangVersionAsserter));
    private readonly IReadOnlyList<IBindingFamilyValidator> _validators = validators?.ToList() ?? throw new ArgumentNullException(nameof(validators));
    private readonly ICakeLog _log = log ?? throw new ArgumentNullException(nameof(log));

    public override async Task RunAsync(BuildContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        AssertLinuxTriplet(context.RuntimeIdentifier, context.Runtime.Triplet);
        _libclangVersionAsserter.Assert();
        LogContainerDigestIfPresent();

        var families = _configRepository.EnumerateEnabledFamilies();
        if (families.Count == 0)
        {
            throw new CakeException(
                "No enabled binding-generation families in manifest. Set binding_generation.enabled=true on at least one library_manifests[] entry.");
        }

        foreach (var familyId in families)
        {
            await GenerateOneFamilyAsync(context, familyId, context.CancellationToken).ConfigureAwait(false);
        }
    }

    private async Task GenerateOneFamilyAsync(BuildContext context, string familyId, CancellationToken ct)
    {
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

        var model = CppAstToBindingModel.Translate(parseResults, config, ConvertRequiredFunctions(config));
        LogPerViewCounts(model);

        await RunFamilyValidatorsAsync(model, config, ct).ConfigureAwait(false);

        var fileSet = CsCommandEmitter.Emit(model, BindingEmissionOptions.FromConfig(config));
        await WriteAsync(context, fileSet, outputDirectory, ct).ConfigureAwait(false);

        _log.Information("Wrote {0} files to '{1}'.", fileSet.Files.Count, outputDirectory.FullPath);
    }

    /// <summary>
    /// Maps <see cref="BindingGenerationConfig.RequiredFunctions"/> (config record shape
    /// with string-typed return + parameter types) to <see cref="BindingFunction"/>
    /// (translator-input shape with <see cref="BindingTypeRef"/>). Phase 3D translator
    /// rewrite will populate <see cref="BindingFunction"/> directly from CppAst types
    /// (and also merge <see cref="BindingGenerationConfig.RequiredFunctions"/> internally),
    /// retiring this bridge entirely.
    /// </summary>
    private static IReadOnlyList<BindingFunction> ConvertRequiredFunctions(BindingGenerationConfig config)
    {
        return [.. config.RequiredFunctions.Select(rf =>
            new BindingFunction(
                Name: rf.Name,
                ReturnType: BindingTypeRef.Of(rf.ReturnType),
                Parameters: [.. rf.Parameters.Select(p => new BindingParameter(BindingTypeRef.Of(p.Type), p.Name))],
                SourceHeader: rf.SourceHeader))];
    }

    private async Task RunFamilyValidatorsAsync(BindingModel model, BindingGenerationConfig config, CancellationToken ct)
    {
        var enabledIds = config.Validators
            .Where(kv => kv.Value)
            .Select(kv => kv.Key)
            .ToHashSet(StringComparer.Ordinal);

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

    private static void AssertLinuxTriplet(string rid, string triplet)
    {
        // EndsWith against the manifest-declared canonical Linux triplet suffix.
        // A bare Contains("linux", ...) substring would accept any malformed
        // triplet that mentions "linux" anywhere (e.g. a hypothetical
        // "linux-bridge-windows"); the suffix check pins the actual triplet
        // shape from build/manifest.json runtimes[linux-*].triplet.
        if (!triplet.EndsWith("-linux-hybrid", StringComparison.OrdinalIgnoreCase))
        {
            throw new CakeException(
                "GenerateBindings is Linux-canonical; expected the host triplet to end with '-linux-hybrid' " +
                "(per build/manifest.json runtimes[linux-x64/linux-arm64].triplet) inside the linux-builder " +
                $"container; got RID '{rid}' / triplet '{triplet}'. Invoke via 'tools.cs generate-bindings'.");
        }
    }

    private void LogContainerDigestIfPresent()
    {
        var digest = Environment.GetEnvironmentVariable("CONTAINER_DIGEST");
        if (!string.IsNullOrWhiteSpace(digest))
        {
            _log.Information("linux-builder container digest: {0}", digest);
        }
    }

    private void LogPerViewCounts(BindingModel model)
    {
        foreach (var view in model.Views)
        {
            _log.Information("View {0}: {1} functions.", view.Name, view.Functions.Count);
        }
    }

    private static async Task WriteAsync(ICakeContext context, GeneratedFileSet fileSet, DirectoryPath outputDirectory, CancellationToken ct)
    {
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
}
