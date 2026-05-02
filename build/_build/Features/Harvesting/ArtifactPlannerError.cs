using Build.Shared.Harvesting;

namespace Build.Features.Harvesting;

public sealed class ArtifactPlannerError : HarvestingError
{
    public ArtifactPlannerError(string message, Exception? exception = null)
        : base(message, exception)
    {
    }
}
