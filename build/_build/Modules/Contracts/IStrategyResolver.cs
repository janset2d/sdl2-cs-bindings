using Build.Context.Models;
using Build.Modules.Strategy.Results;

namespace Build.Modules.Contracts;

public interface IStrategyResolver
{
    StrategyResolutionResult Resolve(RuntimeInfo runtime);
}
