using BigFatFile.Contracts.Hashing;

namespace BigFatFile.Contracts.Chunking;

/// <param name="Offset">Where the chunk starts in the file.</param>
/// <param name="Data">The chunk's bytes. <see cref="ChunkReader"/> reuses this memory for the next chunk.</param>
/// <param name="Hash">The hash of <paramref name="Data"/>.</param>
public readonly record struct Chunk(long Offset, ReadOnlyMemory<byte> Data, ContentHash Hash)
{
    public int Size => Data.Length;
}
