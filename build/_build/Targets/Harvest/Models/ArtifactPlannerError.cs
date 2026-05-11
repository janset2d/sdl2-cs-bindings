namespace Build.Targets.Harvest.Models;

public sealed class ArtifactPlannerError(string message, Exception? exception = null) : HarvestingError(message, exception);
