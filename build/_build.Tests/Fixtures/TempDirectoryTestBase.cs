using System.Collections.Generic;
using System.IO;

namespace Build.Tests.Fixtures;

public abstract class TempDirectoryTestBase
{
    private readonly List<string> _trackedDirectories = [];

    protected string CreateTrackedTempDirectory(string scenario)
    {
        var path = Path.Combine(Path.GetTempPath(), "sdl2-bindings-tests", scenario, Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        _trackedDirectories.Add(path);
        return path;
    }

    [After(Test)]
    public void CleanupTrackedTempDirectories()
    {
        foreach (var path in _trackedDirectories)
        {
            TaskTestHelpers.DeleteDirectoryQuietly(path);
        }

        _trackedDirectories.Clear();
    }
}
