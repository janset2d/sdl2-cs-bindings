using Build.Data.BindingGeneration.Models;
using Cake.Common.IO;
using Cake.Core;
using Cake.Core.IO;

namespace Build.Targets.GenerateBindings.HeaderSet;

// Public because GenerateBindingsTask (sibling Cake Frosting Task convention is
// `public sealed class XxxTask`) takes HeaderSetResolver in its ctor; CS0051
// blocks the internal flip until the wider Task-visibility convention shifts.
public sealed class HeaderSetResolver(ICakeContext context)
{
    private readonly ICakeContext _context = context ?? throw new ArgumentNullException(nameof(context));

    /// <summary>
    /// Resolves the per-family canonical header set from the vcpkg install tree.
    /// All filtering knobs (include-dir glob, header glob, excluded headers, excluded
    /// header prefixes) come from <see cref="BindingGenerationConfig.HeaderSet"/> —
    /// the manifest's <c>binding_generation.header_set</c> block is the single source
    /// of truth.
    /// <para>
    /// The exclusion list documents itself via the manifest entries; the playbook
    /// (<c>docs/playbook/binding-generator-maintenance.md</c> §"Header Set Resolver
    /// Exclusions Maintenance") carries the per-category rationale (SDL.h umbrella,
    /// pragma-pack scaffolding, satellite umbrellas, GL/GLES convenience wrappers,
    /// SDL_test* scaffolding).
    /// </para>
    /// </summary>
    public ResolvedHeaderSet Resolve(
        BindingGenerationConfig config,
        DirectoryPath vcpkgInstalledDirectory,
        DirectoryPath syntheticHeadersDirectory,
        string triplet)
    {
        ArgumentNullException.ThrowIfNull(config);
        ArgumentNullException.ThrowIfNull(vcpkgInstalledDirectory);
        ArgumentNullException.ThrowIfNull(syntheticHeadersDirectory);
        ArgumentException.ThrowIfNullOrWhiteSpace(triplet);

        if (config.HeaderSet is null)
        {
            throw new CakeException(
                $"Family '{config.FamilyId}' has binding_generation.enabled=true but no header_set block. " +
                "Enabled families must declare header_set.include_dir_glob + header_set.header_glob.");
        }

        var headerSet = config.HeaderSet;
        // include_dir_glob is relative to the triplet root (e.g. "include/SDL2"), not
        // to the triplet's include/ subdir. Lets manifest entries declare any
        // out-of-band header layout without resolver assumptions about "include/" being
        // the universal prefix.
        var tripletRoot = vcpkgInstalledDirectory.Combine(triplet);
        var familyIncludeDir = tripletRoot.Combine(headerSet.IncludeDirGlob);
        // IncludeRoot is the parent (the triplet's include/ directory) — CppAst's
        // SystemIncludeFolders consumes this as the resolution root for transitive
        // <SDL2/SDL_video.h>-style header lookups.
        var includeRoot = familyIncludeDir.GetParent();

        if (!_context.DirectoryExists(familyIncludeDir))
        {
            throw new CakeException(
                $"Family '{config.FamilyId}' include directory was not found at '{familyIncludeDir.FullPath}'.");
        }

        var excludedHeaders = headerSet.ExcludedHeaders.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var excludedPrefixes = headerSet.ExcludedHeaderPrefixes.ToList();

        var headers = _context.GetFiles($"{familyIncludeDir.FullPath}/{headerSet.HeaderGlob}")
            .Where(path => !IsExcluded(path.GetFilename().FullPath, excludedHeaders, excludedPrefixes))
            .OrderBy(path => path.FullPath, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return headers.Count != 0
            ? new ResolvedHeaderSet(includeRoot, syntheticHeadersDirectory, familyIncludeDir, headers)
            : throw new CakeException(
                $"No headers matching '{headerSet.HeaderGlob}' for family '{config.FamilyId}' were found at '{familyIncludeDir.FullPath}'.");
    }

    private static bool IsExcluded(string fileName, HashSet<string> excludedHeaders, IReadOnlyList<string> excludedPrefixes)
    {
        return excludedHeaders.Contains(fileName)
            || excludedPrefixes.Any(prefix => fileName.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));
    }
}
