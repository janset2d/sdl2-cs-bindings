namespace Build.Targets.Harvest.Models;

public sealed class CopierError(string message, Exception? exception = null) : HarvestingError(message, exception);
