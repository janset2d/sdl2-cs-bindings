using System.Collections.Immutable;
using Build.Context.Models;
using Build.Modules.Preflight.Models;
using Build.Modules.Preflight.Results;

namespace Build.Modules.Contracts;

public interface IStrategyCoherenceValidator
{
    StrategyCoherenceResult Validate(IImmutableList<RuntimeInfo> runtimes);
}
