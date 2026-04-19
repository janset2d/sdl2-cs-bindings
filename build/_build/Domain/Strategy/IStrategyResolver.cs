using Build.Context.Models;
using Build.Domain.Strategy.Results;

namespace Build.Domain.Strategy;

public interface IStrategyResolver
{
    StrategyResolutionResult Resolve(RuntimeInfo runtime);
}
