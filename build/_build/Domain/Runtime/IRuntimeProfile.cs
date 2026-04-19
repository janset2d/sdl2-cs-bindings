using Cake.Core;
using Cake.Core.IO;

namespace Build.Domain.Runtime;

public interface IRuntimeProfile
{
    string Rid { get; }

    string Triplet { get; }

    PlatformFamily PlatformFamily { get; }

    bool IsSystemFile(FilePath path);
}
