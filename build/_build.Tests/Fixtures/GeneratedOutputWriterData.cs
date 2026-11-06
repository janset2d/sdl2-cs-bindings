using Build.Data.BindingGeneration.Models;
using Cake.Core.IO;

namespace Build.Tests.Fixtures;

public static class GeneratedOutputWriterData
{
    public static IEnumerable<(DirectoryPath? Root, DirectoryPath? Output, IReadOnlyList<GeneratedFile>? Files, string Parameter)> NullInputs =>
    [
        (null, new DirectoryPath("/repo/Generated/family"), [], "root"),
        (new DirectoryPath("/repo/Generated"), null, [], "outputDirectory"),
        (new DirectoryPath("/repo/Generated"), new DirectoryPath("/repo/Generated/family"), null, "files"),
    ];
}
