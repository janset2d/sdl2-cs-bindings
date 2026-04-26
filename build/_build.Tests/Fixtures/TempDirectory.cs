using System.Globalization;
using System.IO;

namespace Build.Tests.Fixtures;

/// <summary>
/// Ephemeral real-disk temp directory rooted under <see cref="Path.GetTempPath"/>.
/// Used by integration tests where the system under test bypasses
/// <c>ICakeContext.FileSystem</c> (e.g. NuGet.Protocol's local folder feed scanner)
/// and needs real files on disk.
/// </summary>
public sealed class TempDirectory : IDisposable
{
    public TempDirectory(string prefix = "janset-temp-")
    {
        Path = System.IO.Path.Combine(
            System.IO.Path.GetTempPath(),
            prefix + Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture));
        Directory.CreateDirectory(Path);
    }

    public string Path { get; }

    public void Dispose()
    {
        if (!Directory.Exists(Path))
        {
            return;
        }

        try
        {
            Directory.Delete(Path, recursive: true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // OS will reclaim; teardown failures must not fail green tests.
        }
    }
}
