using System.Text.RegularExpressions;
using Build.Data.BindingGeneration.Models;
using Build.Host.Cake;
using Build.Host.Paths;
using Build.Results;
using Cake.Common.IO;
using Cake.Core;

namespace Build.Data.BindingGeneration;

/// <summary>
/// Loads SDL2's dynapi manifest (<c>SDL2.exports</c>) from vcpkg's buildtree.
/// <para>
/// vcpkg's binary cache stores compiled artifacts only — <c>installed/&lt;triplet&gt;/&lt;port&gt;/</c>
/// contents. On a binary-cache hit vcpkg unpacks the archive directly and skips source
/// extraction, so <c>buildtrees/sdl2/src/</c> is empty on the run that consumes the cache.
/// The Janset binding generator therefore relies on three reach mechanisms to keep
/// <c>SDL2.exports</c> available regardless of cache state:
/// </para>
/// <list type="bullet">
///   <item><description><b>Local container (binding-generator image):</b> <c>vcpkg install</c>
///   runs at image build time in a dedicated Docker layer; buildtrees lives inside the image.
///   Docker layer cache invalidates on vcpkg.json / overlay / submodule changes.</description></item>
///   <item><description><b>CI (regenerate-bindings.yml workflow):</b> <c>vcpkg-setup</c>
///   action's <c>actions/cache@v5</c> multi-path covers both the binary cache and
///   <c>external/vcpkg/buildtrees/sdl2/src</c>.</description></item>
///   <item><description><b>Host dev machine:</b> <c>tools.cs setup</c> (or any
///   <c>vcpkg install sdl2</c> flow) populates buildtrees once; subsequent invocations
///   preserve it.</description></item>
/// </list>
/// The glob path the repository scans is composed by
/// <see cref="IPathService.GetSdl2DynapiExportsGlob"/>; this class holds no hardcoded paths.
/// </summary>
public interface IDynapiManifestRepository
{
    Task<Result<DynapiManifest, ManifestResolutionError>> LoadAsync(CancellationToken ct = default);
}

/// <inheritdoc cref="IDynapiManifestRepository"/>
public sealed partial class DynapiManifestRepository(ICakeContext context, IPathService paths) : IDynapiManifestRepository
{
    // Watcom-format export line shape (with optional `#` prefix that marks the
    // Watcom DEF-file path as disabled — modern SDL2 dynamic libraries still
    // export those symbols; only the legacy Watcom build path skips them):
    //
    //     ++'_SDL_Init'.'SDL2.dll'.'SDL_Init'
    //   # ++'_SDL_WinRTRunApp'.'SDL2.dll'.'SDL_WinRTRunApp'
    //
    // Both shapes are treated as ACTIVE exports. Pure comments (`# free text`
    // with no `++` marker after the `#`) are skipped.
    [GeneratedRegex(@"^\s*(?:#\s*)?\+\+'_(?<name>[A-Za-z_][A-Za-z0-9_]*)'\.'SDL2\.dll'\.'(?<mirror>[A-Za-z_][A-Za-z0-9_]*)'\s*$", RegexOptions.CultureInvariant | RegexOptions.NonBacktracking)]
    private static partial Regex ExportLine();

    private readonly ICakeContext _context = context ?? throw new ArgumentNullException(nameof(context));
    private readonly IPathService _paths = paths ?? throw new ArgumentNullException(nameof(paths));

    public async Task<Result<DynapiManifest, ManifestResolutionError>> LoadAsync(CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        var globPath = _paths.GetSdl2DynapiExportsGlob();
        var candidates = _context.GetFiles(globPath).ToList();

        if (candidates.Count == 0)
        {
            return Result<DynapiManifest, ManifestResolutionError>.Failure(ManifestResolutionError.NotFound(globPath));
        }

        // Fail-closed on multiple matches: vcpkg's buildtrees layout for sdl2 is
        // <version>-<sha>.clean/. A lex-largest pick would risk selecting a stale
        // leftover from a version downgrade. Better to surface the ambiguity so
        // the operator deletes the stale tree (or runs vcpkg's housekeeping)
        // than to silently bind against the wrong manifest.
        if (candidates.Count > 1)
        {
            var ambiguousPaths = string.Join(", ", candidates.Select(file => $"'{file.FullPath}'"));
            return Result<DynapiManifest, ManifestResolutionError>.Failure(ManifestResolutionError.AmbiguousGlobMatches(globPath, ambiguousPaths));
        }

        var path = candidates[0];
        var content = await _context.ReadAllTextAsync(path).ConfigureAwait(false);

        try
        {
            var manifest = Parse(content, path.FullPath);
            return Result<DynapiManifest, ManifestResolutionError>.Success(manifest);
        }
        catch (InvalidOperationException ex)
        {
            return Result<DynapiManifest, ManifestResolutionError>.Failure(ManifestResolutionError.ParseFailure(path.FullPath, ex.Message));
        }
    }

    internal static DynapiManifest Parse(string content, string sourcePath)
    {
        ArgumentNullException.ThrowIfNull(content);
        ArgumentException.ThrowIfNullOrWhiteSpace(sourcePath);

        var symbols = new HashSet<string>(StringComparer.Ordinal);
        var regex = ExportLine();
        var lineNumber = 0;

        foreach (var rawLine in content.Split('\n'))
        {
            lineNumber++;
            var line = rawLine.TrimEnd('\r');
            var trimmed = line.AsSpan().TrimStart();

            if (trimmed.IsEmpty)
            {
                continue;
            }

            var carriesExportMarker = LooksLikeExportLine(trimmed);
            if (!carriesExportMarker && trimmed[0] == '#')
            {
                // Pure textual comment: skip silently.
                continue;
            }

            var match = regex.Match(line);
            if (!match.Success)
            {
                if (carriesExportMarker)
                {
                    throw new InvalidOperationException(
                        $"Dynapi manifest '{sourcePath}' line {lineNumber}: export line did not match the canonical Watcom format: '{line}'.");
                }
                throw new InvalidOperationException(
                    $"Dynapi manifest '{sourcePath}' line {lineNumber}: unexpected non-comment, non-export line '{line}'.");
            }

            var name = match.Groups["name"].Value;
            var mirror = match.Groups["mirror"].Value;
            if (!string.Equals(name, mirror, StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    $"Dynapi manifest '{sourcePath}' line {lineNumber}: symbol mismatch between underscore-prefix '{name}' and mirror '{mirror}'.");
            }

            symbols.Add(name);
        }

        return new DynapiManifest(symbols, DynapiManifestOrigin.VcpkgBuildtree, sourcePath);
    }

    private static bool LooksLikeExportLine(ReadOnlySpan<char> trimmed)
    {
        if (trimmed.StartsWith("++", StringComparison.Ordinal))
        {
            return true;
        }
        if (trimmed[0] == '#')
        {
            var afterHash = trimmed[1..].TrimStart();
            return afterHash.StartsWith("++", StringComparison.Ordinal);
        }
        return false;
    }
}
