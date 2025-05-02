using Cake.Core;
using Cake.Core.IO;

namespace Build.Tools.Dumpbin;

public class DumpbinDependentsTool(ICakeContext cakeContext)
    : DumpbinTool(cakeContext)
{
    public IList<string>? RunDependents(DumpbinDependentsSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        var builder = new ProcessArgumentBuilder();

        if (!string.IsNullOrWhiteSpace(settings.DllPath))
        {
            builder.Append("/dependents");
            builder.AppendQuoted(settings.DllPath);
        }

        List<string>? dumpbinOut = null;
        Run(settings, builder, new ProcessSettings { RedirectStandardOutput = true }, process => dumpbinOut = [.. process.GetStandardOutput()]);

        if (dumpbinOut == null)
        {
            return [];
        }

        const string startMarker = "Image has the following dependencies:";
        const string endMarker = "Summary";
        const string dllSuffix = ".dll";

        var dependentDlls = dumpbinOut
            .SkipWhile(line => !line.Contains(startMarker, StringComparison.OrdinalIgnoreCase))
            .Skip(1)
            .TakeWhile(line => !line.Contains(endMarker, StringComparison.OrdinalIgnoreCase))
            .Select(line => line.Trim())
            .Where(line => !string.IsNullOrEmpty(line) && line.EndsWith(dllSuffix, StringComparison.OrdinalIgnoreCase))
            .ToList();

        return dependentDlls;
    }
}
