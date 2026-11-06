using Build.Results;
using Cake.Core.IO;

namespace Build.Data.ProjectMetadata;
public interface IProjectMetadataReader
{
    /// <summary>
    /// Resolves MSBuild-evaluated properties (<c>TargetFrameworks</c>, <c>Authors</c>,
    /// <c>PackageLicenseFile</c>, <c>PackageIcon</c>) for the supplied csproj. Returns a typed
    /// <see cref="Result{TValue,TError}"/> carrying either the resolved <see cref="EvaluatedProjectMetadata"/>
    /// or a <see cref="ProjectMetadataError"/> describing the MSBuild or parse failure.
    /// </summary>
    public Result<EvaluatedProjectMetadata, ProjectMetadataError> Read(FilePath projectPath);
}
