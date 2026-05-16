using Build.Data.BindingGeneration;
using Build.Host;
using Build.Host.Cake;
using Build.Targets.GenerateBindings.Emitting;
using Build.Targets.GenerateBindings.HeaderSet;
using Build.Targets.GenerateBindings.Model;
using Build.Targets.GenerateBindings.Parsing;
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
[TaskDescription("Generates Stage 1 SDL2.Core preview bindings (Linux-canonical, runs inside linux-builder container).")]
public sealed class GenerateBindingsTask(
    HeaderSetResolver headerResolver,
    ICppAstParseRunner parseRunner,
    ILibclangVersionAsserter libclangVersionAsserter,
    IDynapiManifestRepository dynapiRepository,
    IBindingPublicApiCoherenceValidator publicApiValidator,
    ICakeLog log) : AsyncFrostingTask<BuildContext>
{
    private readonly HeaderSetResolver _headerResolver = headerResolver ?? throw new ArgumentNullException(nameof(headerResolver));
    private readonly ICppAstParseRunner _parseRunner = parseRunner ?? throw new ArgumentNullException(nameof(parseRunner));
    private readonly ILibclangVersionAsserter _libclangVersionAsserter = libclangVersionAsserter ?? throw new ArgumentNullException(nameof(libclangVersionAsserter));
    private readonly IDynapiManifestRepository _dynapiRepository = dynapiRepository ?? throw new ArgumentNullException(nameof(dynapiRepository));
    private readonly IBindingPublicApiCoherenceValidator _publicApiValidator = publicApiValidator ?? throw new ArgumentNullException(nameof(publicApiValidator));
    private readonly ICakeLog _log = log ?? throw new ArgumentNullException(nameof(log));

    public override async Task RunAsync(BuildContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var triplet = context.Runtime.Triplet;
        AssertLinuxTriplet(context.RuntimeIdentifier, triplet);
        _libclangVersionAsserter.Assert();
        LogContainerDigestIfPresent();

        var vcpkgInstalledRoot = context.Paths.GetVcpkgInstalledDir;
        var syntheticHeadersRoot = context.Paths.BindingGeneratorSyntheticHeadersRoot;
        var outputDirectory = context.Paths.GetGenerateBindingsPreviewFamilyRoot("sdl2-core");

        _log.Information("Generating SDL2.Core preview bindings to '{0}' (triplet '{1}').", outputDirectory.FullPath, triplet);

        var headerSet = _headerResolver.ResolveSdl2CoreHeaders(vcpkgInstalledRoot, syntheticHeadersRoot, triplet);

        _log.Information("Resolved {0} SDL2 headers under '{1}'.", headerSet.Headers.Count, headerSet.Sdl2IncludeDirectory.FullPath);

        var catalog = PlatformCatalog.CreateSdl2Catalog();

        // Pre-announce the parse views BEFORE entering the parallel loop. Cake
        // ICakeLog is not documented thread-safe, so calling _log.Information(...)
        // from inside the PLINQ Select(...) lambda would risk interleaved or
        // corrupted log output across the 8 concurrent view tasks. Logging the
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
            .WithCancellation(context.CancellationToken)
            .Select(view => _parseRunner.Parse(headerSet, view))
            .ToList();

        var sdl2CoreConfig = Sdl2CoreGenerationConfig.Default;
        var model = CppAstToPreviewModel.Translate(parseResults, sdl2CoreConfig.ExcludedFunctionNames, sdl2CoreConfig.RequiredFunctions);
        EnsureNeutralViewNonEmpty(model);
        LogPerViewCounts(model);

        await ValidatePublicApiCoherenceAsync(model, context.CancellationToken).ConfigureAwait(false);

        var fileSet = PreviewEmitter.Emit(model);
        await WriteAsync(context, fileSet, outputDirectory, context.CancellationToken).ConfigureAwait(false);

        _log.Information("Wrote {0} files to '{1}'.", fileSet.Files.Count, outputDirectory.FullPath);
    }

    private async Task ValidatePublicApiCoherenceAsync(PreviewBindingModel model, CancellationToken ct)
    {
        var manifestResult = await _dynapiRepository.LoadAsync(ct).ConfigureAwait(false);
        if (manifestResult.TryGetError(out var error))
        {
            throw new CakeException(error.Reason);
        }

        var manifest = manifestResult.Value;

        _log.Information("Dynapi manifest resolved: {0} ({1} public symbols).", manifest.SourcePath, manifest.PublicSymbols.Count);

        var emittedSymbols = model.Views
            .SelectMany(v => v.Functions)
            .Select(f => f.Name)
            .ToHashSet(StringComparer.Ordinal);

        var report = _publicApiValidator.Validate(emittedSymbols, manifest, SeverityProfile.Stage1Generator);

        foreach (var warning in report.Warnings)
        {
            _log.Warning("{0}: {1}", warning.Name, warning.Message);
        }

        if (!report.IsValid)
        {
            foreach (var err in report.Errors)
            {
                _log.Error("{0}: {1}", err.Name, err.Message);
            }

            throw new CakeException(
                $"BindingPublicApiCoherenceValidator failed: {report.Errors.Count} symbol(s) emitted that SDL2's dynapi manifest does not list as public exports. See preceding log lines for the offending names.");
        }

        _log.Information("Public-API coherence check passed: {0} emitted symbols, {1} warnings (manifest exports missing from emit).",
            emittedSymbols.Count, report.Warnings.Count);
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

    private static void EnsureNeutralViewNonEmpty(PreviewBindingModel model)
    {
        var neutral = model.Views.FirstOrDefault(v => string.Equals(v.Name, "Neutral", StringComparison.Ordinal));
        if (neutral is null || neutral.Functions.Count == 0)
        {
            throw new CakeException(
                "Neutral parse view returned 0 SDL2 functions. Header set or platform macro hygiene likely misconfigured. " +
                "Inspect PlatformCatalog.AllPlatformMacros and verify the SDL2 header tree under vcpkg_installed.");
        }
    }

    private void LogPerViewCounts(PreviewBindingModel model)
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
