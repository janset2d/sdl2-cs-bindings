using Janset.SDL2.PostProcess.Config;

namespace Janset.SDL2.PostProcess;

/// <summary>
/// Owner/consumer mode resolution for the uniform-opaque postprocess step.
/// </summary>
/// <remarks>
/// Decoupled from <c>Program.cs</c> so the cyclomatic complexity of the local
/// functions does not roll up into the <c>&lt;Main&gt;$</c> CA1502 budget. The
/// orchestrator (<c>generate_bindings.py</c>) passes
/// <c>--owner-mode owner|consumer</c> explicitly per family identity; the
/// fallback inside config owner_mode exists only as a deprecation safety net so a
/// missing flag does not silently flip an owner directory to consumer mode (which
/// would lose the <c>Handles.g.cs</c> emit).
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
    /// explicit <c>--owner-mode</c> wins; otherwise fall back to config
    /// owner_mode and log a warning.
    /// </summary>
    public static bool Resolve(string[] arguments, string outputDirectory, FamilyConfig config, string family)
    {
        var flag = ParseFlag(arguments);
        if (flag is { } explicitMode)
        {
            Console.WriteLine($"uniform-opaque: owner-mode={(explicitMode ? "owner" : "consumer")} (explicit --owner-mode flag)");
            return explicitMode;
        }

        Console.WriteLine("uniform-opaque: WARNING — --owner-mode flag not provided; falling back to config owner_mode.");
        var fallback = config.OwnerMode(family);
        Console.WriteLine($"uniform-opaque: owner-mode={(fallback ? "owner" : "consumer")} (config fallback)");
        return fallback;
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
        bool? isOwner,
        string namespaceName)
    {
        if (mode != "uniform-opaque" || handleNames is null || isOwner is not { } resolvedIsOwner)
        {
            return;
        }

        if (resolvedIsOwner)
        {
            var handlesFilePath = Path.Combine(outputDirectory, "Handles.g.cs");
            var sortedNames = handleNames.OrderBy(s => s, StringComparer.Ordinal).ToList();
            var content = OpaqueHandleEmitRewriter.BuildHandlesFileContent(sortedNames, namespaceName);
            Directory.CreateDirectory(outputDirectory);
            PostProcessCli.WriteAllTextLf(handlesFilePath, content);
            Console.WriteLine($"uniform-opaque: wrote {handlesFilePath} with {sortedNames.Count} handles (owner mode)");
        }
        else
        {
            Console.WriteLine($"uniform-opaque: consumer directory ({Path.GetFileName(outputDirectory)}); skipped Handles.g.cs emit");
        }
    }
}
