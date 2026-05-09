using Build.Shared.Harvesting;

namespace Build.Targets.Harvest.Services;

public sealed class ArtifactPlannerError : HarvestingError
{
    public ArtifactPlannerError(string message, Exception? exception = null)
        : base(message, exception)
    {
    }
}
