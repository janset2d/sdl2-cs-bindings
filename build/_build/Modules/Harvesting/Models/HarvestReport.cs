using Cake.Core.IO;

namespace Build.Modules.Harvesting.Models;

public sealed record HarvestReport(FilePath RootBinary, IReadOnlySet<NativeArtifact> Artifacts, IReadOnlySet<string> ProcessedPackageNames);
