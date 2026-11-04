using System.Text.Json;
using BigFatFile.Contracts.Chunking;
using BigFatFile.Contracts.Hashing;

namespace BigFatFile.Contracts.Manifests;

/// <summary>
/// One version of a file: the chunking that cut it, then either its chunks in order or the
/// whole object that holds it, its total size, and the hash of the whole file where known.
/// </summary>
public sealed record FileManifest
{
    public required ManifestChunking Chunking { get; init; }

    /// <summary>The chunks in order. Null when the chunking is <c>none</c>.</summary>
    public IReadOnlyList<ManifestChunk>? Chunks { get; init; }

    /// <summary>The ID of the whole object. Set only when the chunking is <c>none</c>.</summary>
    public Guid? Object { get; init; }

    public required long Size { get; init; }

    public ContentHash? Hash { get; init; }

    public static async Task<FileManifest> CreateAsync(Stream input, CancellationToken cancellationToken = default)
    {
        using var reader = new ChunkReader(input);
        var chunks = new List<ManifestChunk>();
        while (await reader.ReadNextAsync(cancellationToken) is { } chunk)
        {
            chunks.Add(new ManifestChunk(chunk.Hash, chunk.Size));
        }

        return new FileManifest
        {
            Chunking = ManifestChunking.Default,
            Chunks = chunks,
            Size = reader.Length,
            Hash = reader.FileHash,
        };
    }

    /// <summary>
    /// Checks the rules every manifest must follow, whoever produced it. Returns null when
    /// the manifest is valid, otherwise the first rule it breaks. It does not check whether
    /// the server accepts the chunking: that is <see cref="ManifestChunking.Accepted"/>.
    /// </summary>
    public string? FindError()
    {
        if (Size < 0)
        {
            return "The size is negative.";
        }

        if (Chunking.IsNone)
        {
            if (Chunks is not null)
            {
                return "A manifest with the chunking 'none' has no chunks.";
            }

            return Object is null ? "A manifest with the chunking 'none' must name its object." : null;
        }

        if (Object is not null)
        {
            return "Only a manifest with the chunking 'none' names an object.";
        }

        if (Chunks is null)
        {
            return "The manifest has no list of chunks.";
        }

        if (Chunking is not { Gear: not null, MinSize: { } min, AverageSize: { } average, MaxSize: { } max, NormalizationLevel: not null }
            || min < 1 || average < min || max < average)
        {
            return "The chunking needs gear, minSize, averageSize, maxSize and normalizationLevel, with minSize at least 1 and minSize <= averageSize <= maxSize.";
        }

        long total = 0;
        for (var i = 0; i < Chunks.Count; i++)
        {
            var size = Chunks[i].Size;
            if (size < 1 || size > max)
            {
                return $"Chunk {i} has {size} bytes; a chunk has 1 to {max} bytes.";
            }

            if (size < min && i < Chunks.Count - 1)
            {
                return $"Chunk {i} has {size} bytes; only the last chunk may be smaller than {min}.";
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
