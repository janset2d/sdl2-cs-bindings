using Build.Results;

namespace Build.Harvesting;

public abstract class HarvestingError(string message, Exception? exception = null) : BuildError(message, exception);
