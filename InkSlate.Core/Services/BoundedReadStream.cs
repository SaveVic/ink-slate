using System.IO;

namespace InkSlate.Core.Services;

/// <summary>
/// A non-seekable read-only <see cref="Stream"/> wrapper that enforces an upper bound on bytes read.
/// Throws <see cref="InvalidDataException"/> if the read limit or shared cumulative limit is exceeded.
/// Protects against zip bombs, decompression loops, and header-spoofing attacks.
/// </summary>
public sealed class BoundedReadStream : Stream
{
    private readonly Stream _innerStream;
    private readonly long _maxBytes;
    private readonly ArchiveByteTracker? _tracker;
    private long _bytesRead;

    public BoundedReadStream(Stream innerStream, long maxBytes, ArchiveByteTracker? tracker = null)
    {
        ArgumentNullException.ThrowIfNull(innerStream);
        ArgumentOutOfRangeException.ThrowIfNegative(maxBytes);

        _innerStream = innerStream;
        _maxBytes = maxBytes;
        _tracker = tracker;
    }

    public long BytesRead => _bytesRead;
    public long MaxBytes => _maxBytes;

    public override bool CanRead => _innerStream.CanRead;
    public override bool CanSeek => false;
    public override bool CanWrite => false;
    public override long Length => _innerStream.Length;

    public override long Position
    {
        get => _bytesRead;
        set => throw new NotSupportedException("BoundedReadStream does not support seeking.");
    }

    public override int Read(byte[] buffer, int offset, int count)
    {
        var read = _innerStream.Read(buffer, offset, count);
        TrackBytes(read);
        return read;
    }

    public override int Read(Span<byte> buffer)
    {
        var read = _innerStream.Read(buffer);
        TrackBytes(read);
        return read;
    }

    public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
    {
        var read = await _innerStream.ReadAsync(buffer.AsMemory(offset, count), cancellationToken).ConfigureAwait(false);
        TrackBytes(read);
        return read;
    }

    public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
    {
        var read = await _innerStream.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);
        TrackBytes(read);
        return read;
    }

    public override int ReadByte()
    {
        var value = _innerStream.ReadByte();
        if (value != -1)
        {
            TrackBytes(1);
        }
        return value;
    }

    public override void Flush() => _innerStream.Flush();

    public override Task FlushAsync(CancellationToken cancellationToken) =>
        _innerStream.FlushAsync(cancellationToken);

    public override long Seek(long offset, SeekOrigin origin) =>
        throw new NotSupportedException("BoundedReadStream does not support seeking.");

    public override void SetLength(long value) =>
        throw new NotSupportedException("BoundedReadStream does not support setting length.");

    public override void Write(byte[] buffer, int offset, int count) =>
        throw new NotSupportedException("BoundedReadStream is read-only.");

    public override void Write(ReadOnlySpan<byte> buffer) =>
        throw new NotSupportedException("BoundedReadStream is read-only.");

    private void TrackBytes(int read)
    {
        if (read <= 0)
        {
            return;
        }

        _bytesRead += read;
        if (_bytesRead > _maxBytes)
        {
            throw new InvalidDataException(
                $"Archive entry uncompressed size ({_bytesRead} bytes) exceeded maximum allowed quota of {_maxBytes} bytes.");
        }

        _tracker?.RecordBytes(read);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _innerStream.Dispose();
        }
        base.Dispose(disposing);
    }

    public override async ValueTask DisposeAsync()
    {
        await _innerStream.DisposeAsync().ConfigureAwait(false);
        await base.DisposeAsync().ConfigureAwait(false);
    }
}

/// <summary>
/// Thread-safe tracker that enforces a cumulative uncompressed byte quota across multiple entries of an archive.
/// </summary>
public sealed class ArchiveByteTracker
{
    private long _totalBytesRead;
    public long MaxTotalBytes { get; }

    public ArchiveByteTracker(long maxTotalBytes = 500 * 1024 * 1024)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxTotalBytes);
        MaxTotalBytes = maxTotalBytes;
    }

    public long TotalBytesRead => Interlocked.Read(ref _totalBytesRead);

    public void RecordBytes(long bytes)
    {
        if (bytes <= 0)
        {
            return;
        }

        var newTotal = Interlocked.Add(ref _totalBytesRead, bytes);
        if (newTotal > MaxTotalBytes)
        {
            throw new InvalidDataException(
                $"Cumulative uncompressed archive size ({newTotal} bytes) exceeded maximum allowed quota of {MaxTotalBytes / (1024 * 1024)} MB.");
        }
    }
}
