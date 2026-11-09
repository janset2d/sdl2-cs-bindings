using System.ComponentModel;
using System.Diagnostics;
using System.Text;

namespace Build.Tests.Fixtures.RepositoryGates;

internal static class RepositoryFiles
{
    private static readonly Lazy<string> RootPath = new(() => Git("rev-parse", "--show-toplevel").Trim());
    private static readonly Lazy<IReadOnlySet<string>> TrackedPaths = new(() =>
        Git("-C", Root, "ls-files", "-z").Split('\0', StringSplitOptions.RemoveEmptyEntries).ToHashSet(StringComparer.Ordinal));
    private static readonly Lazy<IReadOnlySet<string>> SubmodulePaths = new(() =>
        Git("-C", Root, "ls-files", "-z", "--stage").Split('\0', StringSplitOptions.RemoveEmptyEntries)
            .Where(entry => entry.StartsWith("160000 ", StringComparison.Ordinal))
            .Select(entry => entry[(entry.IndexOf('\t', StringComparison.Ordinal) + 1)..])
            .ToHashSet(StringComparer.Ordinal));

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

    public static string? ReadText(string repoPath)
    {
        try
        {
            return File.ReadAllText(Path.Combine(Root, repoPath));
        }
        catch (IOException error) when (error is FileNotFoundException or DirectoryNotFoundException)
        {
            return null;
        }
    }

    private static string Git(params string[] arguments)
    {
        var startInfo = new ProcessStartInfo("git")
        {
            WorkingDirectory = AppContext.BaseDirectory,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
        };
        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        using var process = new Process { StartInfo = startInfo };
        var stderr = new StringBuilder();
        process.ErrorDataReceived += (_, e) =>
        {
            if (e.Data is not null)
            {
                stderr.AppendLine(e.Data);
            }
        };
        if (!process.Start())
        {
            throw new InvalidOperationException("Could not start git.");
        }

        process.BeginErrorReadLine();
        var stdout = process.StandardOutput.ReadToEnd();
        process.WaitForExit();
        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException(stderr.ToString());
        }

        return stdout;
    }
}
