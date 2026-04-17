namespace Build.Modules.Packaging.Results;

public sealed class PackageFamilySelectionError : PackagingError
{
    public PackageFamilySelectionError(string message, Exception? exception = null)
        : base(message, exception)
    {
    }
}
