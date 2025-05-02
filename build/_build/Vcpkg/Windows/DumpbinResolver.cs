using Cake.Core.IO;

namespace Build.Vcpkg.Windows;

public class DumpbinResolver
{
#pragma warning disable S4487
    private readonly IProcessRunner _processRunner;
#pragma warning restore S4487

    public DumpbinResolver(IProcessRunner processRunner)
    {
        _processRunner = processRunner;
    }


}
