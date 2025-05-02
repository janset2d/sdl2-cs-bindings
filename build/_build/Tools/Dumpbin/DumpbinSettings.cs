using Cake.Core.Tooling;

namespace Build.Tools.Dumpbin;

public class DumpbinSettings : ToolSettings
{
}


public class DumpbinDependentsSettings : DumpbinSettings
{
    public DumpbinDependentsSettings(string dllPath)
    {
        DllPath = dllPath;
    }

    public string DllPath { get; set; }
}
