using Build.Context;
using Cake.Core.Diagnostics;
using Cake.Frosting;

namespace Build.Tasks.Vcpkg;

[TaskName("Vcpkg-Build")]
public class VcpkgInstallTask: AsyncFrostingTask<BuildContext>
{
    public override Task RunAsync(BuildContext context)
    {
        context.Log.Information("VCPKG Installation");
        context.Log.Information("VCPKG Directory: {0}", context.Paths.VcpkgRoot);
        context.Log.Information("VCPKG Triplet: {0}", context.Paths.GetVcpkgInstalledDir("x64-windows"));
        context.Log.Information("VCPKG Libs: {0}", context.Vcpkg.LibrariesToBuild);

        return Task.CompletedTask;
    }
}
