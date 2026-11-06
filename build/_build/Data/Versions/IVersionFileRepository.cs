using Cake.Core.IO;

namespace Build.Data.Versions;

public interface IVersionFileRepository
{
    public PackageFamilyVersionSet Load(FilePath path);

    public Task SaveAsync(FilePath path, PackageFamilyVersionSet versions);
}
