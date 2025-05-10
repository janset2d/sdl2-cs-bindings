namespace Build.Modules.Harvesting.Models;

using System.Collections.Generic;
using Cake.Core.IO;

public sealed record HarvestReport(
    FilePath RootBinary, // The primary binary this report is for
    IReadOnlySet<NativeArtifact> Artifacts,
    IReadOnlySet<string> ProcessedPackageNames // Vcpkg names like "sdl2", "libwebp"
);
