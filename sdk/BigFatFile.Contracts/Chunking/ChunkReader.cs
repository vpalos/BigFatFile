using System.Buffers;
using BigFatFile.Contracts.Hashing;

namespace BigFatFile.Contracts.Chunking;

/// <summary>
/// Reads a stream once and cuts it into chunks, hashing each chunk and the whole file
/// in the same pass. Memory use is one buffer of <see cref="ChunkerParameters.MaxSize"/>.
/// </summary>
public sealed class ChunkReader(Stream input) : IDisposable
{
    private readonly Stream input = input;
    private readonly ContentHasher fileHasher = new();
    private byte[]? buffer = ArrayPool<byte>.Shared.Rent(ChunkerParameters.MaxSize);
    private int filled;
    private int consumed;
    private long offset;
    private bool endOfInput;
    private ContentHash? fileHash;

    /// <summary>The number of bytes in the chunks returned so far.</summary>
    public long Length => offset;

    /// <summary>The hash of the whole file. Available once <see cref="ReadNextAsync"/> has returned null.</summary>
    public ContentHash FileHash =>
        fileHash ?? throw new InvalidOperationException("The file hash is known only after the last chunk.");

    /// <summary>
    /// Returns the next chunk, or null after the last one. The returned chunk's memory is
    /// overwritten by the next call.
    /// </summary>
    public async ValueTask<Chunk?> ReadNextAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(buffer is null, this);

        if (consumed > 0)
        {
            Buffer.BlockCopy(buffer, consumed, buffer, 0, filled - consumed);
            filled -= consumed;
            consumed = 0;
        }

        while (!endOfInput && filled < ChunkerParameters.MaxSize)
        {
            var read = await input.ReadAsync(
                buffer.AsMemory(filled, ChunkerParameters.MaxSize - filled), cancellationToken);
            if (read == 0)
            {
                endOfInput = true;
            }

            filled += read;
        }

        if (filled == 0)
        {
            fileHash ??= fileHasher.Finalize();
            return null;
        }

        var size = FastCdc.FindCut(buffer.AsSpan(0, filled));
        var data = buffer.AsMemory(0, size);
        fileHasher.Update(data.Span);

        var chunk = new Chunk(offset, data, ContentHash.Compute(data.Span));
        offset += size;
        consumed = size;
        return chunk;
    }

    public void Dispose()
    {
        if (buffer is not null)
        {
            ArrayPool<byte>.Shared.Return(buffer);
            buffer = null;
        }

        fileHasher.Dispose();
    }
}
