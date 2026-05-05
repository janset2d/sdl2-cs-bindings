using Build.Versioning;

namespace Build.Repositories;

public interface IVersionFileRepository
{
    PackageFamilyVersionSet Load();
    Task SaveAsync(PackageFamilyVersionSet versions);
}
