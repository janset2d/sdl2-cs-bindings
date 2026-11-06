namespace Build.Targets.Harvest.Models;

public abstract class ClosureError(string message, Exception? exception = null) : HarvestingError(message, exception);
