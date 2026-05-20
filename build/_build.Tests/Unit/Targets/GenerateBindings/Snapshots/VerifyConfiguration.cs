using System.Runtime.CompilerServices;
using VerifyTests;

namespace Build.Tests.Unit.Targets.GenerateBindings.Snapshots;

internal static class VerifyConfiguration
{
    [ModuleInitializer]
    public static void Initialize()
    {
        Verifier.UseProjectRelativeDirectory("Unit/Targets/GenerateBindings/Snapshots");
    }
}
