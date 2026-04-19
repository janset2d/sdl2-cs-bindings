using System.Collections.Immutable;
using Build.Context.Models;
using Build.Domain.Preflight.Results;

namespace Build.Domain.Preflight;

public interface IStrategyCoherenceValidator
{
    StrategyCoherenceResult Validate(IImmutableList<RuntimeInfo> runtimes);
}
