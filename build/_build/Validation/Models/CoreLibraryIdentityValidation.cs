
namespace Build.Validation.Models;
public sealed record CoreLibraryIdentityValidation(CoreLibraryIdentityCheck Check)
{
    public bool HasErrors => !Check.IsValid;
}
