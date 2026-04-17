using Build.Modules.Packaging.Results;
using Cake.Core.IO;

namespace Build.Modules.Contracts;

public interface IProjectMetadataReader
{
    /// <summary>
    /// Resolves MSBuild-evaluated properties (<c>TargetFrameworks</c>, <c>Authors</c>,
    /// <c>PackageLicenseFile</c>, <c>PackageIcon</c>) for the supplied csproj. Returns a typed
    /// <see cref="ProjectMetadataResult"/> carrying either the resolved metadata or a
    /// <see cref="ProjectMetadataError"/> describing the MSBuild or parse failure.
    /// </summary>
    Task<ProjectMetadataResult> ReadAsync(FilePath projectPath, CancellationToken cancellationToken = default);
}
