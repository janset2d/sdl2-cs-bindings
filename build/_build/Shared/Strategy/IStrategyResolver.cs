using Build.Shared.Manifest;

namespace Build.Shared.Strategy;

public interface IStrategyResolver
{
    StrategyResolutionResult Resolve(RuntimeInfo runtime);
}
