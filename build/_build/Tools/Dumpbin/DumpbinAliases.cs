using Cake.Core;

namespace Build.Tools.Dumpbin;

public static class DumpbinAliases
{
    public static IList<string>? DumpbinDependents(this ICakeContext context, DumpbinDependentsSettings settings)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(settings);

        var tool = new DumpbinDependentsTool(context);

        return tool.RunDependents(settings);
    }
}
