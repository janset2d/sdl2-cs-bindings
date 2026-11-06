using System.Buffers.Binary;
using System.IO.Compression;

namespace Build.Tests.Fixtures;

internal sealed class AsyncZipEntryReadStream : Stream
{
    private readonly MemoryStream _storage = new();
    private readonly ZipArchiveMode _mode;
    private readonly int _dataOffset;
    private readonly Action<byte[]> _save;

    public AsyncZipEntryReadStream(byte[] content, ZipArchiveMode mode, Action<byte[]> save)
    {
        _storage.Write(content);
        _storage.Position = 0;
        _mode = mode;
        _save = save;
        // A single-entry fixture's data follows the local header, name and extra fields.
        _dataOffset = 30 + BinaryPrimitives.ReadUInt16LittleEndian(content.AsSpan(26)) + BinaryPrimitives.ReadUInt16LittleEndian(content.AsSpan(28));
    }

    public override bool CanRead => _storage.CanRead;
    public override bool CanSeek => _storage.CanSeek;
    public override bool CanWrite => _storage.CanWrite;
    public override long Length => _storage.Length;
    public override long Position { get => _storage.Position; set => _storage.Position = value; }

    public override int Read(byte[] buffer, int offset, int count) => Read(buffer.AsSpan(offset, count));

    public override int Read(Span<byte> buffer)
    {
        if ((_mode == ZipArchiveMode.Read && Position == 0 && buffer.Length == sizeof(uint)) ||
            (_mode == ZipArchiveMode.Update && Position == _dataOffset))
        {
            throw new IOException("ZIP entry headers must be read asynchronously.");
        }

        return _storage.Read(buffer);
    }

    public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        => _storage.ReadAsync(buffer, cancellationToken);

    public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        => _storage.ReadAsync(buffer, offset, count, cancellationToken);

    public override long Seek(long offset, SeekOrigin origin) => _storage.Seek(offset, origin);
    public override void Flush() => _storage.Flush();
    public override void SetLength(long value) => _storage.SetLength(value);
    public override void Write(byte[] buffer, int offset, int count) => _storage.Write(buffer, offset, count);

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _save(_storage.ToArray());
            _storage.Dispose();
        }

        base.Dispose(disposing);
    }
}
