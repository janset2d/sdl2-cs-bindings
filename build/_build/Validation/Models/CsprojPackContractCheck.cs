
namespace Build.Validation.Models;
public sealed record CsprojPackContractCheck(
    string FamilyIdentifier,
    string CsprojRelativePath,
    CsprojPackContractCheckKind Kind,
    bool IsValid,
    string? ExpectedValue,
    string? ActualValue,
    string? ErrorMessage)
{
    public bool IsError => !IsValid;
}
