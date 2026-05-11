using Build.Results;

namespace Build.Targets.Harvest.Models;

public abstract class HarvestingError(string message, Exception? exception = null) : BuildError(message, exception);
