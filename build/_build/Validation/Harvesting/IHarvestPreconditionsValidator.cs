using Build.Results;

namespace Build.Validation.Harvesting;

/// <summary>
/// Validates Harvest task preconditions before per-library work begins. Currently asserts the
/// vcpkg triplet directory for the active runtime exists; consumers translate an invalid
/// report into a <c>CakeException</c> at the task boundary so failure logging stays in one
/// place.
/// </summary>
public interface IHarvestPreconditionsValidator
{
    ValidationReport Validate();
}
