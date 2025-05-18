using Build.Modules.Harvesting.Models;
using Cake.Core.IO;

namespace Build.Modules.Harvesting.Contracts;

public interface IFilesystemCopier
{
    Task CopyAsync(IEnumerable<NativeArtifact> artifacts, CancellationToken ct = default);
}

public interface IRuntimeProfile
{
    string Rid { get; }

    string Triplet { get; }

    string OsFamily { get; }

    string? CoreLibName { get; }

    bool IsSystemFile(FilePath path);
}
