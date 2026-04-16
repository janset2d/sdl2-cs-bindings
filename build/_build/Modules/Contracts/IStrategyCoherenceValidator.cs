using System.Collections.Immutable;
using Build.Context.Models;
using Build.Modules.Preflight.Models;

namespace Build.Modules.Contracts;

public interface IStrategyCoherenceValidator
{
    StrategyCoherenceValidation Validate(IImmutableList<RuntimeInfo> runtimes);
}
