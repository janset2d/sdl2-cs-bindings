using Build.Packaging;
using Build.Results;
using Cake.Core.IO;

namespace Build.Packaging;

public interface IProjectMetadataReader
{
    /// <summary>
    /// Resolves MSBuild-evaluated properties (<c>TargetFrameworks</c>, <c>Authors</c>,
    /// <c>PackageLicenseFile</c>, <c>PackageIcon</c>) for the supplied csproj. Returns a typed
    /// <see cref="Result{TValue,TError}"/> carrying either the resolved <see cref="ProjectMetadata"/>
    /// or a <see cref="ProjectMetadataError"/> describing the MSBuild or parse failure.
    /// </summary>
    Task<Result<ProjectMetadata, ProjectMetadataError>> ReadAsync(FilePath projectPath, CancellationToken ct = default);
}
