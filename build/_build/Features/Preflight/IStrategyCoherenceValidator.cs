using System.Collections.Immutable;
using Build.Shared.Manifest;

namespace Build.Features.Preflight;

public interface IStrategyCoherenceValidator
{
    StrategyCoherenceResult Validate(IImmutableList<RuntimeInfo> runtimes);
}
