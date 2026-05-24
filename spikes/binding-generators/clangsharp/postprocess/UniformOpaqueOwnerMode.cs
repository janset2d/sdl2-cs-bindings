namespace Janset.SDL2.PostProcess;

/// <summary>
/// Owner/consumer mode resolution for the uniform-opaque postprocess step.
/// </summary>
/// <remarks>
/// Decoupled from <c>Program.cs</c> so the cyclomatic complexity of the local
/// functions does not roll up into the <c>&lt;Main&gt;$</c> CA1502 budget. The
/// orchestrator (<c>generate_bindings.py</c>) passes
/// <c>--owner-mode owner|consumer</c> explicitly per family identity; the
/// substring-based fallback inside <see cref="IsOwnerDirectoryByPath"/> exists
/// only as a deprecation safety net so a missing flag does not silently flip
/// an owner directory to consumer mode (which would lose the
/// <c>Handles.g.cs</c> emit).
/// </remarks>
internal static class UniformOpaqueOwnerMode
{
    /// <summary>
    /// Parse the optional <c>--owner-mode owner|consumer</c> CLI flag. Returns
    /// null if the flag is absent or its value is not one of the two
    /// recognized tokens.
    /// </summary>
    public static bool? ParseFlag(string[] arguments)
    {
        for (int i = 0; i < arguments.Length - 1; i++)
        {
            if (arguments[i] == "--owner-mode")
            {
                return arguments[i + 1].ToLowerInvariant() switch
                {
                    "owner" => true,
                    "consumer" => false,
                    _ => null,
                };
            }
        }
        return null;
    }

    /// <summary>
    /// Resolve uniform-opaque owner/consumer mode for the current invocation:
    /// explicit <c>--owner-mode</c> wins; otherwise fall back to substring
    /// detection on the output directory and log a deprecation warning so the
    /// missing orchestrator wire-up is visible in the build log instead of
    /// silently corrupting the emit.
    /// </summary>
    public static bool Resolve(string[] arguments, string outputDirectory)
    {
        var flag = ParseFlag(arguments);
        if (flag is { } explicitMode)
        {
            Console.WriteLine($"uniform-opaque: owner-mode={(explicitMode ? "owner" : "consumer")} (explicit --owner-mode flag)");
            return explicitMode;
        }

        Console.WriteLine("uniform-opaque: WARNING — --owner-mode flag not provided; falling back to substring detection. Pass --owner-mode owner|consumer explicitly.");
        var fallback = IsOwnerDirectoryByPath(outputDirectory);
        Console.WriteLine($"uniform-opaque: owner-mode={(fallback ? "owner" : "consumer")} (substring fallback)");
        return fallback;
    }

    /// <summary>
    /// Path-based handle-owner detection. <c>Janset.SDL2.Core</c> declares the
    /// Pattern B bodies; <c>Janset.SDL2.Image</c> and any future satellite
    /// consume them via ProjectReference + shared SDL2 namespace nesting.
    /// </summary>
    /// <remarks>
    /// Kept as a fallback only. A future project rename (e.g.,
    /// <c>Janset.SDL2.Core</c> -> <c>Janset.SDL2.SDL2</c>) would silently flip
    /// every directory to consumer mode under this check, which is why the
    /// orchestrator now wires <c>--owner-mode</c> explicitly. Remove once
    /// every caller is confirmed to pass the flag.
    /// </remarks>
    public static bool IsOwnerDirectoryByPath(string directory)
    {
        return directory.Replace('\\', '/').Contains("/Janset.SDL2.Core/", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Slice C-B Phase 2 (orchestrator pass): owner directories receive a
    /// single consolidated <c>Handles.g.cs</c> holding the canonical Pattern B
    /// body for every handle in the roster. Consumer directories skip the
    /// write because they reference the owner's Handles.g.cs via
    /// ProjectReference + shared SDL2 namespace.
    /// </summary>
    public static void EmitConsolidatedHandlesFileIfOwner(
        string mode,
        string outputDirectory,
        HashSet<string>? handleNames,
        bool? isOwner)
    {
        if (mode != "uniform-opaque" || handleNames is null || isOwner is not { } resolvedIsOwner)
        {
            return;
        }

        if (resolvedIsOwner)
        {
            var handlesFilePath = Path.Combine(outputDirectory, "Handles.g.cs");
            var sortedNames = handleNames.OrderBy(s => s, StringComparer.Ordinal).ToList();
            var content = OpaqueHandleEmitRewriter.BuildHandlesFileContent(sortedNames);
            Directory.CreateDirectory(outputDirectory);
            File.WriteAllText(handlesFilePath, content);
            Console.WriteLine($"uniform-opaque: wrote {handlesFilePath} with {sortedNames.Count} handles (owner mode)");
        }
        else
        {
            Console.WriteLine($"uniform-opaque: consumer directory ({Path.GetFileName(outputDirectory)}); skipped Handles.g.cs emit");
        }
    }
}
