using Cake.Core.Tooling;

namespace Build.Tools.Dumpbin;

public class DumpbinSettings : ToolSettings
{
    public DumpbinSettings(string dllPath)
    {
        DllPath = dllPath;
    }

    public string DllPath { get; set; }
}
