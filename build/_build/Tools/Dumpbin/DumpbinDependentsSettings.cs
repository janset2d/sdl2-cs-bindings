using Cake.Core.Tooling;

namespace Build.Tools.Dumpbin;


public class DumpbinSettings : ToolSettings
{
}


public class DumpbinDependentsSettings : DumpbinSettings
{
    public DumpbinDependentsSettings(string dependentsPath)
    {
        DependentsPath = dependentsPath;
    }

    public string DependentsPath { get; }
}
