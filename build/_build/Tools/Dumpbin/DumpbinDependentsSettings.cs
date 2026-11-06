namespace Build.Tools.Dumpbin;

public class DumpbinDependentsSettings : DumpbinSettings
{
    public DumpbinDependentsSettings(string dependentsPath)
    {
        DependentsPath = dependentsPath;
    }

    public string DependentsPath { get; }
}
