using OneOf;
using OneOf.Monads;
using OneOf.Types;

namespace Build.Features.Packaging;

/// <summary>
/// Result monad for <c>IProjectMetadataReader.ReadAsync</c>:
/// <list type="bullet">
///   <item><term>Success</term><description><see cref="ProjectMetadata"/> — MSBuild-evaluated properties</description></item>
///   <item><term>Error</term><description><see cref="ProjectMetadataError"/> — MSBuild invocation / parse failures</description></item>
/// </list>
/// </summary>
public sealed class ProjectMetadataResult(OneOf<Error<ProjectMetadataError>, Success<ProjectMetadata>> result)
    : Result<ProjectMetadataError, ProjectMetadata>(result)
{
    public static implicit operator ProjectMetadataResult(ProjectMetadataError error) => new(new Error<ProjectMetadataError>(error));
    public static implicit operator ProjectMetadataResult(ProjectMetadata metadata) => new(new Success<ProjectMetadata>(metadata));

    public static explicit operator ProjectMetadataError(ProjectMetadataResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        return result.AsT0.Value;
    }

    public static explicit operator ProjectMetadata(ProjectMetadataResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        return result.AsT1.Value;
    }

    public static ProjectMetadataResult FromProjectMetadataError(ProjectMetadataError error) => error;
    public static ProjectMetadataResult FromProjectMetadata(ProjectMetadata metadata) => metadata;

    public static ProjectMetadataError ToProjectMetadataError(ProjectMetadataResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        return result.AsT0.Value;
    }

    public static ProjectMetadata ToProjectMetadata(ProjectMetadataResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        return result.AsT1.Value;
    }

    public ProjectMetadata ProjectMetadata => SuccessValue();

    public ProjectMetadataError ProjectMetadataError => AsT0.Value;
}
