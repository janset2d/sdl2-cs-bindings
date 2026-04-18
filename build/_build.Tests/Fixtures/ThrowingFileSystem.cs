// S1144 false positive: interface-implementation members on private nested classes are
// required for IFile/IDirectory contract satisfaction even if the test code itself never
// touches them directly.
#pragma warning disable S1144

using Cake.Core.IO;
using CakePath = Cake.Core.IO.Path;

namespace Build.Tests.Fixtures;

/// <summary>
/// Decorator around an existing <see cref="IFileSystem"/> that raises a caller-supplied
/// exception when specific file or directory operations are invoked against paths
/// matching a predicate. Purpose: deterministically prove that mid-flight I/O failures
/// leave old state intact (staged-replace invariant) without needing a real crash.
/// <para>
/// Usage: wrap a <c>FakeRepoBuilder.FileSystem</c> and swap it into the test's
/// <c>ICakeContext.FileSystem.Returns(...)</c> setup.
/// </para>
/// </summary>
public sealed class ThrowingFileSystem : IFileSystem
{
    private readonly IFileSystem _inner;
    private readonly Predicate<ThrowTrigger> _shouldThrow;
    private readonly Func<ThrowTrigger, Exception> _exceptionFactory;

    public ThrowingFileSystem(IFileSystem inner, Predicate<ThrowTrigger> shouldThrow, Func<ThrowTrigger, Exception> exceptionFactory)
    {
        _inner = inner ?? throw new ArgumentNullException(nameof(inner));
        _shouldThrow = shouldThrow ?? throw new ArgumentNullException(nameof(shouldThrow));
        _exceptionFactory = exceptionFactory ?? throw new ArgumentNullException(nameof(exceptionFactory));
    }

    public IFile GetFile(FilePath path)
    {
        ArgumentNullException.ThrowIfNull(path);
        return new ThrowingFile(_inner.GetFile(path), path, _shouldThrow, _exceptionFactory);
    }

    public IDirectory GetDirectory(DirectoryPath path)
    {
        ArgumentNullException.ThrowIfNull(path);
        return new ThrowingDirectory(_inner.GetDirectory(path), path, _shouldThrow, _exceptionFactory);
    }

    private sealed class ThrowingFile : IFile
    {
        private readonly IFile _inner;
        private readonly FilePath _path;
        private readonly Predicate<ThrowTrigger> _shouldThrow;
        private readonly Func<ThrowTrigger, Exception> _exceptionFactory;

        public ThrowingFile(IFile inner, FilePath path, Predicate<ThrowTrigger> shouldThrow, Func<ThrowTrigger, Exception> exceptionFactory)
        {
            _inner = inner;
            _path = path;
            _shouldThrow = shouldThrow;
            _exceptionFactory = exceptionFactory;
        }

        public FilePath Path => _inner.Path;

        CakePath IFileSystemInfo.Path => _inner.Path;

        public bool Exists => _inner.Exists;

        public bool Hidden => _inner.Hidden;

        public long Length => _inner.Length;

        public FileAttributes Attributes
        {
            get => _inner.Attributes;
            set => _inner.Attributes = value;
        }

        public void Copy(FilePath destination, bool overwrite)
        {
            ThrowIfConfigured(ThrowOperation.FileCopy, destination);
            _inner.Copy(destination, overwrite);
        }

        public void Delete() => _inner.Delete();

        public void Move(FilePath destination)
        {
            ThrowIfConfigured(ThrowOperation.FileMove, destination);
            _inner.Move(destination);
        }

        public Stream Open(FileMode fileMode, FileAccess fileAccess, FileShare fileShare)
        {
            ThrowIfConfigured(ThrowOperation.FileOpen, destination: null, fileMode);
            return _inner.Open(fileMode, fileAccess, fileShare);
        }

        public IFile SetCreationTime(DateTime creationTime) => _inner.SetCreationTime(creationTime);

        public IFile SetCreationTimeUtc(DateTime creationTimeUtc) => _inner.SetCreationTimeUtc(creationTimeUtc);

        public IFile SetLastAccessTime(DateTime lastAccessTime) => _inner.SetLastAccessTime(lastAccessTime);

        public IFile SetLastAccessTimeUtc(DateTime lastAccessTimeUtc) => _inner.SetLastAccessTimeUtc(lastAccessTimeUtc);

        public IFile SetLastWriteTime(DateTime lastWriteTime) => _inner.SetLastWriteTime(lastWriteTime);

        public IFile SetLastWriteTimeUtc(DateTime lastWriteTimeUtc) => _inner.SetLastWriteTimeUtc(lastWriteTimeUtc);

        public IFile SetUnixFileMode(UnixFileMode unixFileMode) => _inner.SetUnixFileMode(unixFileMode);

        private void ThrowIfConfigured(ThrowOperation operation, FilePath? destination, FileMode? fileMode = null)
        {
            var trigger = new ThrowTrigger(operation, _path, destination, fileMode);
            if (_shouldThrow(trigger))
            {
                throw _exceptionFactory(trigger);
            }
        }
    }

    private sealed class ThrowingDirectory : IDirectory
    {
        private readonly IDirectory _inner;
        private readonly DirectoryPath _path;
        private readonly Predicate<ThrowTrigger> _shouldThrow;
        private readonly Func<ThrowTrigger, Exception> _exceptionFactory;

        public ThrowingDirectory(IDirectory inner, DirectoryPath path, Predicate<ThrowTrigger> shouldThrow, Func<ThrowTrigger, Exception> exceptionFactory)
        {
            _inner = inner;
            _path = path;
            _shouldThrow = shouldThrow;
            _exceptionFactory = exceptionFactory;
        }

        public DirectoryPath Path => _inner.Path;

        CakePath IFileSystemInfo.Path => _inner.Path;

        public bool Exists => _inner.Exists;

        public bool Hidden => _inner.Hidden;

        public void Create() => _inner.Create();

        public void Delete(bool recursive) => _inner.Delete(recursive);

        public IEnumerable<IDirectory> GetDirectories(string filter, SearchScope scope) => _inner.GetDirectories(filter, scope);

        public IEnumerable<IFile> GetFiles(string filter, SearchScope scope) => _inner.GetFiles(filter, scope);

        public void Move(DirectoryPath destination)
        {
            ThrowIfConfigured(ThrowOperation.DirectoryMove, destination);
            _inner.Move(destination);
        }

        public IDirectory SetCreationTime(DateTime creationTime) => _inner.SetCreationTime(creationTime);

        public IDirectory SetCreationTimeUtc(DateTime creationTimeUtc) => _inner.SetCreationTimeUtc(creationTimeUtc);

        public IDirectory SetLastAccessTime(DateTime lastAccessTime) => _inner.SetLastAccessTime(lastAccessTime);

        public IDirectory SetLastAccessTimeUtc(DateTime lastAccessTimeUtc) => _inner.SetLastAccessTimeUtc(lastAccessTimeUtc);

        public IDirectory SetLastWriteTime(DateTime lastWriteTime) => _inner.SetLastWriteTime(lastWriteTime);

        public IDirectory SetLastWriteTimeUtc(DateTime lastWriteTimeUtc) => _inner.SetLastWriteTimeUtc(lastWriteTimeUtc);

        public IDirectory SetUnixFileMode(UnixFileMode unixFileMode) => _inner.SetUnixFileMode(unixFileMode);

        private void ThrowIfConfigured(ThrowOperation operation, DirectoryPath destination)
        {
            var trigger = new ThrowTrigger(operation, _path, destination, FileMode: null);
            if (_shouldThrow(trigger))
            {
                throw _exceptionFactory(trigger);
            }
        }
    }
}

public enum ThrowOperation
{
    FileOpen,
    FileMove,
    FileCopy,
    DirectoryMove,
}

public sealed record ThrowTrigger(ThrowOperation Operation, CakePath SourcePath, CakePath? DestinationPath, FileMode? FileMode);
