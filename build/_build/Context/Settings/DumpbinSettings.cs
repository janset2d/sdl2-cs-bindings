using System.Collections.ObjectModel;

namespace Build.Context.Settings;

public class DumpbinSettings
{
    public IReadOnlyList<string> DllToDump { get; init; }

    public DumpbinSettings(IReadOnlyList<string> dllToDump)
    {
        DllToDump = new ReadOnlyCollection<string>(dllToDump?.ToList() ?? []);
    }
}
