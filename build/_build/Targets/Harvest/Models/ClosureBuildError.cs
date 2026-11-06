
namespace Build.Targets.Harvest.Models;
public sealed class ClosureBuildError(string message, Exception? exception = null) : ClosureError(message, exception);
