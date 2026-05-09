using Build.Results;

namespace Build.Validation.NativeSmoke;

/// <summary>
/// Validates NativeSmoke task preconditions before harness invocation: project directory,
/// CMakeLists.txt, and CMakePresets.json must all be present in the working tree. Consumers
/// translate an invalid report into a <c>CakeException</c> at the task boundary.
/// </summary>
public interface INativeSmokePreconditionsValidator
{
    ValidationReport Validate();
}
