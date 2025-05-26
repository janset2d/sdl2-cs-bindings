using Cake.Core;
using Cake.Core.IO;

namespace Build.Modules.Contracts;

public interface IRuntimeProfile
{
    string Rid { get; }

    string Triplet { get; }

    PlatformFamily PlatformFamily { get; }

    bool IsSystemFile(FilePath path);
}
