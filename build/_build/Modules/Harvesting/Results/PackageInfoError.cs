namespace Build.Modules.Harvesting.Results;

public sealed class PackageInfoError : HarvestingError
{
    public PackageInfoError(string message, Exception? exception = null)
        : base(message, exception)
    {
    }
}
