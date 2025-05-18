using Cake.Core.IO;

namespace Build.Modules.Contracts;

public interface IRuntimeProfile
{
    string Rid { get; }

    string Triplet { get; }

    string OsFamily { get; }

    string? CoreLibName { get; }

    bool IsSystemFile(FilePath path);
}
