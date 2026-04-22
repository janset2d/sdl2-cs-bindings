using System.Globalization;
using System.IO;
using LibGit2Sharp;

namespace Build.Tests.Fixtures;

/// <summary>
/// Ephemeral LibGit2Sharp-backed git repository rooted under <see cref="Path.GetTempPath"/>.
/// Used by integration tests for <c>GitTagVersionProvider</c> (plan §11 Q17) that cannot
/// ride <c>FakeFileSystem</c> — Cake.Frosting.Git bypasses <c>ICakeContext.FileSystem</c>
/// and operates on real disk via LibGit2Sharp's native binary.
/// <para>
/// Provides the minimum surface needed by the Slice C provider tests: Init + commit a
/// synthetic file + apply an annotated tag at HEAD. Teardown deletes the temp directory
/// tree, including the <c>.git</c> folder.
/// </para>
/// </summary>
public sealed class TempGitRepo : IDisposable
{
    private static readonly Signature TestSignature = new(
        "Janset Test",
        "test@janset.local",
        new DateTimeOffset(2026, 4, 21, 0, 0, 0, TimeSpan.Zero));

    public TempGitRepo()
    {
        Path = System.IO.Path.Combine(
            System.IO.Path.GetTempPath(),
            "janset-temp-git-" + Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture));
        Directory.CreateDirectory(Path);
        Repository.Init(Path);
    }

    public string Path { get; }

    /// <summary>
    /// Stage a synthetic file with <paramref name="fileName"/> + <paramref name="content"/>
    /// and commit it. Returns the new HEAD commit SHA.
    /// </summary>
    public string CommitFile(string fileName, string content, string message)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);
        ArgumentNullException.ThrowIfNull(content);
        ArgumentException.ThrowIfNullOrWhiteSpace(message);

        using var repo = new Repository(Path);
        var relativeDir = System.IO.Path.GetDirectoryName(fileName);
        if (!string.IsNullOrEmpty(relativeDir))
        {
            Directory.CreateDirectory(System.IO.Path.Combine(Path, relativeDir));
        }

        File.WriteAllText(System.IO.Path.Combine(Path, fileName), content);
        Commands.Stage(repo, fileName);
        var commit = repo.Commit(message, TestSignature, TestSignature);
        return commit.Sha;
    }

    /// <summary>
    /// Apply a lightweight tag with <paramref name="tagName"/> at HEAD.
    /// </summary>
    public void TagHead(string tagName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tagName);
        using var repo = new Repository(Path);
        repo.ApplyTag(tagName);
    }

    /// <summary>
    /// Apply a lightweight tag at the specified commit SHA.
    /// </summary>
    public void TagAt(string tagName, string commitSha)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tagName);
        ArgumentException.ThrowIfNullOrWhiteSpace(commitSha);
        using var repo = new Repository(Path);
        repo.ApplyTag(tagName, commitSha);
    }

    public string HeadSha
    {
        get
        {
            using var repo = new Repository(Path);
            return repo.Head.Tip.Sha;
        }
    }

    public void Dispose()
    {
        if (!Directory.Exists(Path))
        {
            return;
        }

        // .git internals can have read-only flags on Windows; normalise before delete.
        foreach (var file in Directory.GetFiles(Path, "*", SearchOption.AllDirectories))
        {
            try
            {
                File.SetAttributes(file, FileAttributes.Normal);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // Best effort; fall through to delete attempt.
            }
        }

        try
        {
            Directory.Delete(Path, recursive: true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Teardown failures must never fail a green test; temp dir will be reclaimed
            // by the OS eventually.
        }
    }
}
