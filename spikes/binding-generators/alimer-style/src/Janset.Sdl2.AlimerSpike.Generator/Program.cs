using CppAst;
using Janset.Sdl2.AlimerSpike.Generator;

var options = Sdl2SpikeOptions.FromArgs(args);

if (options.Emit && options.CleanOutput && Directory.Exists(options.GeneratedRoot))
{
    Directory.Delete(options.GeneratedRoot, recursive: true);
}

var coreHeaders = HeaderScopeReader.ReadHeaderPaths(options.Sdl2IncludeRoot, options.CoreScopeFile);
var imageHeaders = HeaderScopeReader.ReadHeaderPaths(options.Sdl2IncludeRoot, options.ImageScopeFile);

Console.WriteLine("Alimer-style CppAst spike");
Console.WriteLine($"  Repository root: {options.RepositoryRoot}");
Console.WriteLine($"  Triplet: {options.VcpkgTriplet}");
Console.WriteLine($"  Scope: {options.Scope}");
Console.WriteLine($"  Mode: {(options.Emit ? "emit" : "parse-only")}");
Console.WriteLine($"  SDL2.Core scoped headers: {coreHeaders.Count}");
Console.WriteLine($"  SDL2_image scoped headers: {imageHeaders.Count}");

var parser = new CppAstParseRunner();
var parseDiagnostics = new List<string>();

var coreCompilations = ParseHeaders("core", coreHeaders);
var imageCompilations = ParseHeaders("image", imageHeaders);

// Cross-cutting exclusions (constitution §"Current accepted deferrals" +
// manifest.json deferred_declarations + the SDL_RWFromFP / variadic family
// the spike does not attempt to bind).
var coreExclusions = new[]
{
    "SDL_GetWindowWMInfo",
    "SDL_RWFromFP",
    "SDL_LogMessageV",
    "SDL_vsnprintf",
    "SDL_vsscanf",
    "SDL_vasprintf",
    "SDL_CreateThread",
    "SDL_CreateThreadWithStackSize",
};

var coreResult = Sdl2RawAbiEmitter.Emit(
    options,
    new EmissionRequest("core", "Sdl2Native", "SDL2", "SDL_", coreExclusions),
    coreCompilations);

var imageResult = Sdl2RawAbiEmitter.Emit(
    options,
    new EmissionRequest("image", "Sdl2ImageNative", "SDL2_image", "IMG_", []),
    imageCompilations);

var stats = new Dictionary<string, FamilyStats>
{
    ["core"] = new(coreHeaders.Count, coreResult.Seen, coreResult.Emitted, coreResult.Skips.Count),
    ["image"] = new(imageHeaders.Count, imageResult.Seen, imageResult.Emitted, imageResult.Skips.Count),
};

var allSkips = coreResult.Skips.Concat(imageResult.Skips).ToList();
Sdl2GenerationReport.Write(options, stats, allSkips, parseDiagnostics);

Console.WriteLine($"core: seen={coreResult.Seen}, emitted={coreResult.Emitted}, skipped={coreResult.Skips.Count}");
Console.WriteLine($"image: seen={imageResult.Seen}, emitted={imageResult.Emitted}, skipped={imageResult.Skips.Count}");
return 0;

List<CppCompilation> ParseHeaders(string family, IReadOnlyList<string> headers)
{
    var compilations = new List<CppCompilation>(headers.Count);
    foreach (var header in headers)
    {
        try
        {
            var compilation = parser.ParseHeader(header, options.Sdl2IncludeRoot);
            compilations.Add(compilation);
            if (compilation.HasErrors)
            {
                foreach (var message in compilation.Diagnostics.Messages.Take(3))
                {
                    parseDiagnostics.Add($"[{family}] {Path.GetFileName(header)}: {message}");
                }
            }
        }
        catch (Exception ex)
        {
            parseDiagnostics.Add($"[{family}] {Path.GetFileName(header)}: parse exception {ex.GetType().Name}: {ex.Message}");
        }
    }
    return compilations;
}
