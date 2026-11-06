namespace Build.Tools.Dumpbin;

public sealed class DumpbinDependentsSettings : DumpbinSettings
{
    public DumpbinDependentsSettings(string dependentsPath)
    {
        DependentsPath = dependentsPath;
    }

    public string DependentsPath { get; }
}
