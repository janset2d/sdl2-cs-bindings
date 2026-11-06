using Build.Data.BindingGeneration.Models;
using Cake.Core.IO;

namespace Build.Data.BindingGeneration;
public interface IGeneratedStampRepository
{
    public Task<GeneratedStamp> LoadAsync(DirectoryPath outputDirectory, CancellationToken ct = default);

    public Task SaveAsync(DirectoryPath outputDirectory, GeneratedStamp stamp, CancellationToken ct = default);
}
