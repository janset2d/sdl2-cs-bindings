using NuGet.Protocol.Core.Types;

namespace Build.Targets.PublishStaging.Services;

internal static class NuGetResources
{
    public static async Task<T> GetRequiredAsync<T>(SourceRepository repository, CancellationToken cancellationToken = default)
        where T : class, INuGetResource
    {
        return await repository.GetResourceAsync<T>(cancellationToken)
            ?? throw new InvalidOperationException($"The source '{repository.PackageSource.Source}' does not provide {typeof(T).Name}.");
    }
}
