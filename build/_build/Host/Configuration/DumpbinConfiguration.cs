using System.Collections.ObjectModel;

namespace Build.Host.Configuration;

public class DumpbinConfiguration
{
    public IReadOnlyList<string> DllToDump { get; init; }

    public DumpbinConfiguration(IReadOnlyList<string> dllToDump)
    {
        DllToDump = new ReadOnlyCollection<string>(dllToDump?.ToList() ?? []);
    }
}
