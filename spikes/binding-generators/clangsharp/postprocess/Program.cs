using Janset.SDL2.PostProcess;
using Microsoft.CodeAnalysis.CSharp;

// Usage:
//   dotnet run --project postprocess -- strip-varargs      <input-dir> [<output-dir>]
//   dotnet run --project postprocess -- libraryimport      <input-dir> [<output-dir>]
//   dotnet run --project postprocess -- platform-delta     <input-dir> [<output-dir>]
//   dotnet run --project postprocess -- guid-substitute    <input-dir> [<output-dir>]
//   dotnet run --project postprocess -- threadid-dispatch  <input-dir> [<output-dir>]
//   dotnet run --project postprocess -- uniform-opaque     <input-dir> [<output-dir>] [--owner-mode owner|consumer]
//
// strip-varargs     : Constitution L162-176 fmt-only policy — drops `__arglist`
//                     parameter from variadic P/Invokes (applied to both Compat
//                     and Modern output before libraryimport).
// libraryimport     : Constitution L48 backend split — promotes [DllImport] to
//                     [LibraryImport] + [UnmanagedCallConv] + partial (applied to
//                     Modern output only; Compat keeps DllImport for legacy TFMs).
// platform-delta    : SDL2 platform-view pass cleanup — removes declarations that
//                     already exist in neutral/earlier platform output and adds
//                     guarded [SupportedOSPlatform] attributes by Platforms/<View>.
// guid-substitute   : Slice C-C — removes the generated `partial struct SDL_GUID`
//                     and rewrites every reference to System.Guid. Wire size is
//                     bit-identical (both 16 bytes); see GuidSubstitutionRewriter
//                     for the ABI trade-off rationale.
// threadid-dispatch : Slice C-A R2 structural — replaces the SDL_ThreadID /
//                     SDL_GetThreadID single P/Invoke with a mode-aware emit:
//                     Modern (net6+) gets [LibraryImport] + CULong; Compat
//                     (netstandard2.0/net462) gets Microsoft's documented dual
//                     DllImport + RuntimeInformation dispatch. Mode is detected
//                     from the input directory path segment (Compat vs Modern),
//                     mirroring PlatformDeltaPostProcessor; no #if directives
//                     are emitted because the csproj already routes
//                     Generated/Compat to legacy TFMs and Generated/Modern to
//                     net6+ via conditional <Compile Include>.
// uniform-opaque    : Slice C-B Pattern B uniform opaque handle emit. Two
//                     channels: (1) auto-detect — empty `partial struct SDL_X {}`
//                     declarations referenced via [NativeTypeName("X *")]
//                     elsewhere; (2) force-opaque — Constitution-bound allow-list
//                     (SDL_RWops, SDL_SysWMinfo, SDL_SysWMmsg). Both channels
//                     emit the same Pattern B shape (readonly partial struct
//                     wrapping nint with IEquatable<T>, explicit operators only)
//                     and rewrite single-pointer X* references in raw ABI
//                     signatures to by-value X (double-pointer / out preserved).
//
// Default behavior is in-place edit; pass an explicit output directory to
// write to a different location.

if (args is ["--self-test"])
{
    return PostProcessSelfTests.Run();
}

if (args.Length < 2 || args[0] is not ("strip-varargs" or "libraryimport" or "platform-delta" or "guid-substitute" or "threadid-dispatch" or "uniform-opaque"))
{
    Console.Error.WriteLine("usage: dotnet run --project postprocess -- <strip-varargs|libraryimport|platform-delta|guid-substitute|threadid-dispatch|uniform-opaque> <input-dir> [<output-dir>] or --self-test");
    return 1;
}

var mode = args[0];
var inputDir = Path.GetFullPath(args[1]);
// args[2] is an optional output directory unless it starts with `--`, in which
// case the third positional is omitted and args[2..] are CLI flags (e.g.
// `--owner-mode owner`). Without this guard a flag would be misparsed as the
// output directory and the postprocess would write Handles.g.cs into a path
// named `--owner-mode/`.
var outputDir = PostProcessCli.ResolveOutputDirectory(args, inputDir);

if (!Directory.Exists(inputDir))
{
    Console.Error.WriteLine($"input directory not found: {inputDir}");
    return 2;
}

var modeInputError = PostProcessCli.ValidateModeInput(mode, inputDir);
if (modeInputError is not null)
{
    Console.Error.WriteLine(modeInputError);
    return 3;
}

if (mode == "platform-delta")
{
    var result = new PlatformDeltaPostProcessor().Process(inputDir, outputDir);
    Console.WriteLine($"platform-delta: {result.ProcessedFiles} platform files scanned, {result.TransformedFiles} files transformed, {result.RemovedDeclarations} duplicate declarations removed");
    return 0;
}

CSharpSyntaxRewriter rewriter;
Func<bool> hasChanges;
Action resetRewriter;
// uniform-opaque carries the canonical handle name set out of the switch so the
// post-loop block (orchestrator phase) can emit a consolidated Handles.g.cs in
// owner directories.
HashSet<string>? uniformOpaqueHandleNames = null;
// uniform-opaque owner/consumer resolution: explicit --owner-mode flag wins
// over the substring-based fallback. Resolved before the switch so the
// post-loop block uses the same value the switch case logged.
bool? uniformOpaqueIsOwner = null;
switch (mode)
{
    case "strip-varargs":
    {
        var r = new StripVarargsRewriter();
        rewriter = r;
        hasChanges = () => r.AnyChanges;
        resetRewriter = r.Reset;
        break;
    }
    case "libraryimport":
    {
        var r = new DllImportToLibraryImportRewriter();
        rewriter = r;
        hasChanges = () => r.AnyChanges;
        resetRewriter = r.Reset;
        break;
    }
    case "guid-substitute":
    {
        var r = new GuidSubstitutionRewriter();
        rewriter = r;
        hasChanges = () => r.AnyChanges;
        resetRewriter = r.Reset;
        break;
    }
    case "threadid-dispatch":
    {
        var threadIdMode = ThreadIdDualDispatchRewriter.DetectMode(inputDir);
        Console.WriteLine($"threadid-dispatch: mode={threadIdMode}");
        var r = new ThreadIdDualDispatchRewriter(threadIdMode);
        rewriter = r;
        hasChanges = () => r.AnyChanges;
        resetRewriter = r.Reset;
        break;
    }
    case "uniform-opaque":
    {
        // Resolve roster path: walk inputDir ancestors until we find the spike policy dir.
        var rosterPath = ResolveOpaqueHandleRosterPath(inputDir);
        var (rosterAutoDetect, rosterForceOpaque) = OpaqueHandleEmitRewriter.LoadRoster(rosterPath);

        // Syntactic discovery acts as a watchdog against the roster (the policy authority).
        var syntacticDetect = OpaqueHandleEmitRewriter.DiscoverAutoDetectedHandles(inputDir);
        OpaqueHandleEmitRewriter.ReportDrift(syntacticDetect, rosterAutoDetect);

        // Combined handle set: rewriter removes any partial struct declaration with
        // one of these names and rewrites SDL_X* -> SDL_X at param/return positions.
        var handleNames = new HashSet<string>(rosterAutoDetect, StringComparer.Ordinal);
        foreach (var n in rosterForceOpaque)
        {
            handleNames.Add(n);
        }

        // Owner/consumer mode resolution. Prefer the explicit --owner-mode CLI
        // flag (set by generate_bindings.py per family identity). Fall back to
        // the substring-based detection with a deprecation warning so a missing
        // orchestrator wire-up does not silently corrupt the emit.
        uniformOpaqueIsOwner = UniformOpaqueOwnerMode.Resolve(args, outputDir);

        Console.WriteLine($"uniform-opaque: applying {rosterAutoDetect.Count} auto-detect + {rosterForceOpaque.Count} force-opaque handles from {Path.GetFileName(rosterPath)} (syntactic discovery: {syntacticDetect.Count})");
        uniformOpaqueHandleNames = handleNames;
        var r = new OpaqueHandleEmitRewriter(handleNames);
        rewriter = r;
        hasChanges = () => r.AnyChanges;
        resetRewriter = r.Reset;
        break;
    }
    default:
        throw new InvalidOperationException($"unknown mode: {mode}");
}

var processed = 0;
var transformed = 0;

PostProcessCli.ProcessDirectory(inputDir, outputDir, rewriter, hasChanges, resetRewriter, ref processed, ref transformed);

Console.WriteLine($"{mode}: {processed} files scanned, {transformed} files transformed");

// Slice C-B Phase 2 (orchestrator pass): owner directories receive a single
// consolidated Handles.g.cs holding the canonical Pattern B body for every
// handle in the roster. Consumer directories (e.g. Janset.SDL2.Image, which
// references Core handles via ProjectReference and nested namespace lookup)
// skip the write — they only need the partial-struct removal + pointer
// rewrite that the loop above already performed. See UniformOpaqueOwnerMode
// for the owner/consumer resolution path (extracted to keep <Main>$ inside
// the CA1502 cyclomatic-complexity ceiling).
UniformOpaqueOwnerMode.EmitConsolidatedHandlesFileIfOwner(mode, outputDir, uniformOpaqueHandleNames, uniformOpaqueIsOwner);

return 0;

static string ResolveOpaqueHandleRosterPath(string inputDir)
{
    var dir = new DirectoryInfo(Path.GetFullPath(inputDir));
    while (dir != null)
    {
        var candidate = Path.Combine(dir.FullName, "spikes", "binding-generators", "clangsharp", "policy", "opaque-handle-roster.json");
        if (File.Exists(candidate))
        {
            return candidate;
        }
        dir = dir.Parent;
    }
    throw new FileNotFoundException(
        $"Could not locate opaque-handle-roster.json by walking ancestors of '{inputDir}'. " +
        "Expected at <repo>/spikes/binding-generators/clangsharp/policy/opaque-handle-roster.json.");
}
