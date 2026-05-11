namespace Build.Targets.Harvest.Models;

public abstract class ClosureError(string message, Exception? exception = null) : HarvestingError(message, exception);

public sealed class ClosureNotFound(string message) : ClosureError(message);

public sealed class ClosureBuildError(string message, Exception? exception = null) : ClosureError(message, exception);
