using Build.Targets.Harvest.Models;
using Cake.Core.IO;

namespace Build.Tests.Fixtures;

public static class ArchiveDeploymentData
{
    public const string FileListGlob = "C:/repo/.cake/temp/filelists/*.txt";

    public static ArchiveCreationAction Action { get; } = new(
        new FilePath("C:/output/native.tar.gz"),
        new DirectoryPath("C:/vcpkg/lib"),
        [new(new FilePath("C:/vcpkg/lib/zlib.so"), "zlib", ArtifactOrigin.Runtime),
            new(new FilePath("C:/vcpkg/lib/SDL2.so"), "sdl2", ArtifactOrigin.Primary)],
        "native.tar.gz");

    public static string ExpectedFileList => $"zlib.so{Environment.NewLine}SDL2.so{Environment.NewLine}";

    public static string ReadFileList(FakeCakeWorld world)
    {
        ArgumentNullException.ThrowIfNull(world);
        var files = world.CakeContext.Globber.GetFiles(FileListGlob);
        using var stream = world.FileSystem.GetFile(files.Single()).OpenRead();
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
