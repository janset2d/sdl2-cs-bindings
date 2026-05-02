using Cake.Core;
using Cake.Core.Annotations;

namespace Build.Tools.Dumpbin;

[CakeAliasCategory("Dumpbin")] // Optional: Add category
public static class DumpbinAliases
{
    /// <summary>
    /// Runs dumpbin /dependents for the specified DLL.
    /// </summary>
    /// <param name="context">The Cake context.</param>
    /// <param name="settings">The settings, including the path to the DLL.</param>
    /// <returns>The raw standard output lines from the dumpbin command, or null.</returns>
    /// <example>
    /// <code>
    /// // In Build Context / Setup
    /// var dumpbinSettings = new DumpbinDependentsSettings("path/to/MyLibrary.dll");
    ///
    /// // In Task
    /// IEnumerable&lt;string&gt; outputLines = DumpbinDependents(dumpbinSettings);
    /// if (outputLines != null)
    /// {
    ///     // Process lines...
    /// }
    /// </code>
    /// </example>
    [CakeMethodAlias]
    public static string? DumpbinDependents(this ICakeContext context, DumpbinDependentsSettings settings)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(settings);

        var tool = new DumpbinDependentsTool(context);

        return tool.RunDependents(settings);
    }
}
