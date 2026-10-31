using System.Text.Json;
using BigFatFile.Contracts.Chunking;
using BigFatFile.Contracts.Hashing;

namespace BigFatFile.Contracts.Manifests;

/// <summary>
/// One version of a file: its chunks in order, its total size and the hash of the whole file.
/// </summary>
public sealed record FileManifest(IReadOnlyList<ManifestChunk> Chunks, long Size, ContentHash Hash)
{
    public static async Task<FileManifest> CreateAsync(Stream input, CancellationToken cancellationToken = default)
    {
        using var reader = new ChunkReader(input);
        var chunks = new List<ManifestChunk>();
        while (await reader.ReadNextAsync(cancellationToken) is { } chunk)
        {
            chunks.Add(new ManifestChunk(chunk.Hash, chunk.Size));
        }

        return new FileManifest(chunks, reader.Length, reader.FileHash);
    }

    /// <summary>
    /// Checks the rules every manifest must follow, whoever produced it. Returns null when
    /// the manifest is valid, otherwise the first rule it breaks.
    /// </summary>
    public string? FindError()
    {
        if (Size < 0)
        {
            return "The size is negative.";
        }

        long total = 0;
        for (var i = 0; i < Chunks.Count; i++)
        {
            var size = Chunks[i].Size;
            if (size is <= 0 or > ChunkerParameters.MaxSize)
            {
                return $"Chunk {i} has {size} bytes; a chunk has 1 to {ChunkerParameters.MaxSize} bytes.";
            }

            if (size < ChunkerParameters.MinSize && i < Chunks.Count - 1)
            {
                return $"Chunk {i} has {size} bytes; only the last chunk may be smaller than {ChunkerParameters.MinSize}.";
            }

            total += size;
        }

        return total == Size ? null : $"The chunks add up to {total} bytes, not {Size}.";
    }

    public string ToJson() => JsonSerializer.Serialize(this, ContractsJsonContext.Default.FileManifest);

    public static FileManifest FromJson(string json) =>
        JsonSerializer.Deserialize(json, ContractsJsonContext.Default.FileManifest)
        ?? throw new JsonException("The manifest is null.");
}
