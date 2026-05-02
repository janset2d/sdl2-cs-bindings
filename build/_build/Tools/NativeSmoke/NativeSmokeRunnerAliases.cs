using Cake.Core;
using Cake.Core.Annotations;

namespace Build.Tools.NativeSmoke;

[CakeAliasCategory("NativeSmoke")]
public static class NativeSmokeRunnerAliases
{
    /// <summary>
    /// Runs the native-smoke executable and captures output.
    /// </summary>
    [CakeMethodAlias]
    public static NativeSmokeRunnerResult NativeSmokeRun(this ICakeContext context, NativeSmokeRunnerSettings settings)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(settings);

        var tool = new NativeSmokeRunnerTool(context);
        return tool.RunSmoke(settings);
    }
}
