using Build.Results;
using Cake.Core.IO;

namespace Build.Validation.Packaging;

/// <summary>
/// Validates the prerequisites for the PackageConsumerSmoke target before any dotnet
/// invocation: smoke csproj exists, compile-sanity csproj exists, local feed directory
/// exists. Returns a <see cref="ValidationReport"/> so the task can translate
/// errors at the boundary; collaborators below the task should not throw.
/// </summary>
public interface IPackageConsumerSmokePreconditionsValidator
{
    ValidationReport Validate(FilePath smokeCsproj, FilePath compileSanityCsproj, DirectoryPath feedPath);
}
