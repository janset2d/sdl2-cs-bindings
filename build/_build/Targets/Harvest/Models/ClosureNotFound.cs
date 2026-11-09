namespace Build.Targets.Harvest.Models;

public sealed class ClosureNotFound(string message) : ClosureError(message);
