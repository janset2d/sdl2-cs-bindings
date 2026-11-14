using Build.Data.BindingGeneration.Models;
using Cake.Common.IO;
using Cake.Core;
using Cake.Core.IO;

namespace Build.Targets.GenerateBindings.HeaderSet;

/// <summary>Keeps manifest-selected headers separate from supplemental inputs.</summary>
public sealed class HeaderSetResolver(ICakeContext context)
{
    private readonly ICakeContext _context = context ?? throw new ArgumentNullException(nameof(context));

    /// <summary>
    /// Resolves the per-family canonical header set from the vcpkg install tree.
    /// The explicit inventory is sorted by its declared order, never by discovery order.
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
                "Enabled families must declare header_set.include_dir + header_set.headers.");
        }

        var headerSet = config.HeaderSet;
        var tripletRoot = vcpkgInstalledDirectory.Combine(triplet);
        var familyIncludeDir = tripletRoot.Combine(headerSet.IncludeDir);
        // IncludeRoot is the parent directory used for transitive
        // <SDL2/SDL_video.h>-style header lookups.
        var includeRoot = familyIncludeDir.GetParent();

        if (!_context.DirectoryExists(familyIncludeDir))
        {
            throw new CakeException(
                $"Family '{config.FamilyId}' include directory was not found at '{familyIncludeDir.FullPath}'.");
        }

        var headers = headerSet.Headers
            .OrderBy(header => header.Order)
            .ThenBy(header => header.Name, StringComparer.Ordinal)
            .Select(header => familyIncludeDir.CombineWithFilePath(header.Name))
            .ToList();

        var missing = headers.FirstOrDefault(header => !_context.FileExists(header));

        if (missing is not null)
        {
            throw new CakeException($"Family '{config.FamilyId}' requested header was not found at '{missing.FullPath}'.");
        }

        return new ResolvedHeaderSet(includeRoot, syntheticHeadersDirectory, familyIncludeDir, headers);
    }
}
