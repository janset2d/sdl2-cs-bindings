using OneOf;
using OneOf.Types;

namespace Build.Modules.Harvesting.Results;

[GenerateOneOf]
public sealed partial  class CopierResult : OneOfBase<CopierError, Success>
{
    public bool IsError => IsT0; // T0 = CopierError
    public bool IsSuccess => IsT1; // T1 = Success

    public CopierError Error => AsT0; // T2 = HarvestingError
}
