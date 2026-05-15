using Cake.Common.IO;
using Cake.Core;
using Cake.Core.IO;

namespace Build.Targets.GenerateBindings.HeaderSet;

internal sealed class HeaderSetResolver(ICakeContext context)
{
    private const string Sdl2HeaderGlob = "*.h";
    private readonly ICakeContext _context = context ?? throw new ArgumentNullException(nameof(context));

    public ResolvedHeaderSet ResolveSdl2CoreHeaders(DirectoryPath vcpkgInstalledDirectory, string triplet)
    {
        ArgumentNullException.ThrowIfNull(vcpkgInstalledDirectory);
        ArgumentException.ThrowIfNullOrWhiteSpace(triplet);

        var includeRoot = vcpkgInstalledDirectory.Combine(triplet).Combine("include");
        var sdl2Include = includeRoot.Combine("SDL2");
        if (!_context.DirectoryExists(sdl2Include))
        {
            throw new CakeException($"SDL2 include directory was not found at '{sdl2Include.FullPath}'.");
        }

        var headers = _context.GetFiles($"{sdl2Include.FullPath}/{Sdl2HeaderGlob}")
            .OrderBy(path => path.FullPath, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return headers.Count != 0
            ? new ResolvedHeaderSet(includeRoot, sdl2Include, headers)
            : throw new CakeException($"No SDL2 headers matching '{Sdl2HeaderGlob}' were found at '{sdl2Include.FullPath}'.");
    }
}
