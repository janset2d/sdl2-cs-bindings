namespace Build.Modules.Harvesting.Models;

using Cake.Core.IO;

public sealed record NativeArtifact(string FileName, FilePath SourcePath, FilePath TargetPath, string PackageName, ArtifactOrigin Origin);

public sealed record ArtifactPlan(IReadOnlySet<NativeArtifact> Artifacts);

public enum ArtifactOrigin
{
    Primary,
    Runtime,
    Metadata,
    License,
}
