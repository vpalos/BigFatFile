namespace BigFatFile.Contracts.Hashing;

/// <summary>
/// Computes a <see cref="ContentHash"/> over bytes that arrive in pieces.
/// </summary>
public sealed class ContentHasher : IDisposable
{
    private readonly Blake3.Hasher hasher = Blake3.Hasher.New();

    public void Update(ReadOnlySpan<byte> data) => hasher.Update(data);

    public ContentHash Finalize()
    {
        Span<byte> output = stackalloc byte[ContentHash.ByteLength];
        hasher.Finalize(output);
        return ContentHash.FromBytes(output);
    }

    public void Dispose() => hasher.Dispose();
}
