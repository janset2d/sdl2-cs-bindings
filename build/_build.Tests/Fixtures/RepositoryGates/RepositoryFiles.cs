using System.ComponentModel;
using System.Diagnostics;

namespace Build.Tests.Fixtures.RepositoryGates;

internal static class RepositoryFiles
{
    private static readonly Lazy<string> RootPath = new(() => Git("rev-parse", "--show-toplevel").Trim());
    private static readonly Lazy<IReadOnlySet<string>> TrackedPaths = new(() =>
        Git("-C", Root, "ls-files", "-z").Split('\0', StringSplitOptions.RemoveEmptyEntries).ToHashSet(StringComparer.Ordinal));
    private static readonly Lazy<IReadOnlySet<string>> SubmodulePaths = new(() =>
        Git("-C", Root, "ls-files", "-z", "--stage").Split('\0', StringSplitOptions.RemoveEmptyEntries)
            .Where(entry => entry.StartsWith("160000 ", StringComparison.Ordinal))
            .Select(entry => entry[(entry.IndexOf('\t', StringComparison.Ordinal) + 1)..]).ToHashSet(StringComparer.Ordinal));

    public static string Root => RootPath.Value;
    public static IReadOnlySet<string> Tracked => TrackedPaths.Value;
    public static IReadOnlySet<string> Submodules => SubmodulePaths.Value;

    public static bool IsAvailable
    {
        get
        {
            try
            {
                return Git("rev-parse", "--is-inside-work-tree").Trim() == "true";
            }
            catch (InvalidOperationException)
            {
                return false;
            }
            catch (Win32Exception)
            {
                return false;
            }
        }
    }

    public static bool Resolves(string repoPath) => repoPath == "." || Tracked.Contains(repoPath)
        || Tracked.Any(path => path.StartsWith(repoPath.TrimEnd('/') + "/", StringComparison.Ordinal))
        || Submodules.Any(path => repoPath.StartsWith(path + "/", StringComparison.Ordinal));

    public static string ReadText(string repoPath) => File.ReadAllText(Path.Combine(Root, repoPath));

    private static string Git(params string[] arguments)
    {
        var startInfo = new ProcessStartInfo("git")
        {
            WorkingDirectory = AppContext.BaseDirectory,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        foreach (var argument in arguments)
            startInfo.ArgumentList.Add(argument);
        using var process = Process.Start(startInfo) ?? throw new InvalidOperationException("Could not start git.");
        var stdout = process.StandardOutput.ReadToEnd();
        var stderr = process.StandardError.ReadToEnd();
        process.WaitForExit();
        if (process.ExitCode != 0)
            throw new InvalidOperationException(stderr);
        return stdout;
    }
}
