namespace Build.Domain.Harvesting.Results;

public sealed class ArtifactPlannerError : HarvestingError
{
    public ArtifactPlannerError(string message, Exception? exception = null)
        : base(message, exception)
    {
    }
}
