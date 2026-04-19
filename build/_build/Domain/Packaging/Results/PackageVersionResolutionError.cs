namespace Build.Domain.Packaging.Results;

public sealed class PackageVersionResolutionError : PackagingError
{
    public PackageVersionResolutionError(string message, string? rawInput = null, Exception? exception = null)
        : base(message, exception)
    {
        RawInput = rawInput;
    }

    /// <summary>
    /// The raw <c>--family-version</c> CLI value that could not be resolved, when available.
    /// Null when the input was missing entirely.
    /// </summary>
    public string? RawInput { get; }
}
