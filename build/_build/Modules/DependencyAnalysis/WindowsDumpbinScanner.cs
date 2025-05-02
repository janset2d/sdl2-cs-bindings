using Build.Tools.Dumpbin;

namespace Build.Modules.DependencyAnalysis;

public class WindowsDumpbinScanner : IDependencyScanner
{
#pragma warning disable S4487
    private readonly DumpbinTool _dumpbinTool;
#pragma warning restore S4487

    public WindowsDumpbinScanner(DumpbinDependentsTool dumpbinTool)
    {
        _dumpbinTool = dumpbinTool;
    }
}
