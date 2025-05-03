using Cake.Core;
using Cake.Core.IO;

namespace Build.Tools.Dumpbin;

public class DumpbinDependentsTool(ICakeContext cakeContext) : DumpbinTool(cakeContext)
{
    /// <summary>
    /// Runs dumpbin /dependents and returns the raw standard output lines.
    /// </summary>
    /// <param name="settings">The settings containing the DllPath.</param>
    /// <returns>An IEnumerable containing the raw standard output lines, or null if execution failed to produce output.</returns>
    public void RunDependents(DumpbinDependentsSettings settings) // Changed return type
    {
        ArgumentNullException.ThrowIfNull(settings);
        if (string.IsNullOrWhiteSpace(settings.DependentsPath))
        {
            throw new InvalidOperationException("DependentsPath cannot be null or empty.");
        }

        var builder = new ProcessArgumentBuilder();
        builder.Append("/dependents");
        builder.AppendQuoted(settings.DependentsPath);

        Run(settings, builder);
    }
}
