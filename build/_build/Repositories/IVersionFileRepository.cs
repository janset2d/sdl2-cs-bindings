using Build.Versioning;
using Cake.Core.IO;

namespace Build.Repositories;

public interface IVersionFileRepository
{
    PackageFamilyVersionSet Load(FilePath path);
    Task SaveAsync(FilePath path, PackageFamilyVersionSet versions);
}
