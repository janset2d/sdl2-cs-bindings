
namespace Build.Validation.Models;
public sealed record VersionConsistencyValidation(IReadOnlyList<LibraryVersionCheck> Checks)
{
    public bool HasErrors => Checks.Any(check => check.IsError);

    public int CheckedLibraries => Checks.Count;
}
