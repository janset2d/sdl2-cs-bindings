using Build.Context.Models;
using Build.Domain.Runtime;
using Build.Domain.Strategy;
using Build.Domain.Strategy.Models;

namespace Build.Application.Packaging;

public sealed class PackagingStrategyFactory(
    ManifestConfig manifest,
    RuntimeConfig runtimeConfig,
    IRuntimeProfile runtimeProfile,
    IStrategyResolver strategyResolver)
{
    private readonly ManifestConfig _manifest = manifest ?? throw new ArgumentNullException(nameof(manifest));
    private readonly RuntimeConfig _runtimeConfig = runtimeConfig ?? throw new ArgumentNullException(nameof(runtimeConfig));
    private readonly IRuntimeProfile _runtimeProfile = runtimeProfile ?? throw new ArgumentNullException(nameof(runtimeProfile));
    private readonly IStrategyResolver _strategyResolver = strategyResolver ?? throw new ArgumentNullException(nameof(strategyResolver));

    public IPackagingStrategy Create()
    {
        var coreLibraryName = _manifest.CoreLibrary.VcpkgName;

        var runtime = _runtimeConfig.Runtimes.SingleOrDefault(r => string.Equals(r.Rid, _runtimeProfile.Rid, StringComparison.Ordinal))
            ?? throw new InvalidOperationException(
                $"RID '{_runtimeProfile.Rid}' was not found in manifest runtimes during strategy resolution.");

        var resolution = _strategyResolver.Resolve(runtime);
        if (resolution.IsError())
        {
            throw new InvalidOperationException(resolution.ResolutionError.Message, resolution.ResolutionError.Exception);
        }

        return resolution.ResolvedModel switch
        {
            PackagingModel.HybridStatic => new HybridStaticStrategy(coreLibraryName),
            PackagingModel.PureDynamic => new PureDynamicStrategy(coreLibraryName),
            _ => throw new InvalidOperationException($"Unsupported packaging model '{resolution.ResolvedModel}'."),
        };
    }
}
