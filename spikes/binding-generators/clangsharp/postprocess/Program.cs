using Janset.SDL2.PostProcess;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

// Usage:
//   dotnet run --project postprocess -- strip-varargs   <input-dir> [<output-dir>]
//   dotnet run --project postprocess -- libraryimport   <input-dir> [<output-dir>]
//   dotnet run --project postprocess -- platform-delta  <input-dir> [<output-dir>]
//   dotnet run --project postprocess -- guid-substitute <input-dir> [<output-dir>]
//
// strip-varargs  : Constitution L162-176 fmt-only policy — drops `__arglist`
//                  parameter from variadic P/Invokes (applied to both Compat
//                  and Modern output before libraryimport).
// libraryimport  : Constitution L48 backend split — promotes [DllImport] to
//                  [LibraryImport] + [UnmanagedCallConv] + partial (applied to
//                  Modern output only; Compat keeps DllImport for legacy TFMs).
// platform-delta : SDL2 platform-view pass cleanup — removes declarations that
//                  already exist in neutral/earlier platform output and adds
//                  guarded [SupportedOSPlatform] attributes by Platforms/<View>.
// guid-substitute: Slice C-C — removes the generated `partial struct SDL_GUID`
//                  and rewrites every reference to System.Guid. Wire size is
//                  bit-identical (both 16 bytes); see GuidSubstitutionRewriter
//                  for the ABI trade-off rationale.
//
// Default behavior is in-place edit; pass an explicit output directory to
// write to a different location.

if (args.Length < 2 || args[0] is not ("strip-varargs" or "libraryimport" or "platform-delta" or "guid-substitute"))
{
    Console.Error.WriteLine("usage: dotnet run --project postprocess -- <strip-varargs|libraryimport|platform-delta|guid-substitute> <input-dir> [<output-dir>]");
    return 1;
}

var mode = args[0];
var inputDir = Path.GetFullPath(args[1]);
var outputDir = args.Length >= 3 ? Path.GetFullPath(args[2]) : inputDir;

if (!Directory.Exists(inputDir))
{
    Console.Error.WriteLine($"input directory not found: {inputDir}");
    return 2;
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
    default:
        throw new InvalidOperationException($"unknown mode: {mode}");
}

var processed = 0;
var transformed = 0;

foreach (var file in Directory.EnumerateFiles(inputDir, "*.g.cs", SearchOption.AllDirectories))
{
    var relative = Path.GetRelativePath(inputDir, file);
    var source = File.ReadAllText(file);
    var tree = CSharpSyntaxTree.ParseText(source);
    var root = tree.GetCompilationUnitRoot();

    resetRewriter();
    var rewritten = (CompilationUnitSyntax)rewriter.Visit(root)!;
    processed++;

    if (!hasChanges())
    {
        if (!string.Equals(inputDir, outputDir, StringComparison.OrdinalIgnoreCase))
        {
            var passthrough = Path.Combine(outputDir, relative);
            Directory.CreateDirectory(Path.GetDirectoryName(passthrough)!);
            File.WriteAllText(passthrough, source);
        }
        continue;
    }

    var destination = Path.Combine(outputDir, relative);
    Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
    File.WriteAllText(destination, rewritten.ToFullString());
    transformed++;
}

Console.WriteLine($"{mode}: {processed} files scanned, {transformed} files transformed");
return 0;
