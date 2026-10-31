using BigFatFile.Contracts.Chunking;
using BigFatFile.Contracts.Hashing;
using BigFatFile.Contracts.Manifests;

namespace BigFatFile.Contracts.Tests;

public sealed class FileManifestTests
{
    private static readonly ContentHash SomeHash = ContentHash.Compute([1, 2, 3]);

    [Fact]
    public void JsonHasTheDocumentedShape()
    {
        var manifest = new FileManifest([new ManifestChunk(SomeHash, 3)], 3, SomeHash);

        Assert.Equal(
            $$"""{"chunks":[{"hash":"{{SomeHash}}","size":3}],"size":3,"hash":"{{SomeHash}}"}""",
            manifest.ToJson());
    }

    [Fact]
    public void JsonRoundTrips()
    {
        var manifest = new FileManifest(
            [new ManifestChunk(SomeHash, ChunkerParameters.MinSize), new ManifestChunk(SomeHash, 7)],
            ChunkerParameters.MinSize + 7,
            SomeHash);

        Assert.Equal(manifest.ToJson(), FileManifest.FromJson(manifest.ToJson()).ToJson());
    }

    [Fact]
    public void AnEmptyFileHasNoChunks()
    {
        Assert.Null(new FileManifest([], 0, SomeHash).FindError());
    }

    [Fact]
    public void ChunksMustAddUpToTheSize()
    {
        Assert.NotNull(new FileManifest([new ManifestChunk(SomeHash, 3)], 4, SomeHash).FindError());
    }

    [Fact]
    public void OnlyTheLastChunkMayBeSmallerThanTheMinimum()
    {
        var manifest = new FileManifest(
            [new ManifestChunk(SomeHash, 7), new ManifestChunk(SomeHash, ChunkerParameters.MinSize)],
            ChunkerParameters.MinSize + 7,
            SomeHash);

        Assert.NotNull(manifest.FindError());
    }

    [Fact]
    public void NoChunkMayExceedTheMaximum()
    {
        var size = ChunkerParameters.MaxSize + 1;

        Assert.NotNull(new FileManifest([new ManifestChunk(SomeHash, size)], size, SomeHash).FindError());
    }

    [Fact]
    public void MissingPropertiesAreRejected()
    {
        Assert.ThrowsAny<System.Text.Json.JsonException>(() => FileManifest.FromJson("""{"chunks":[],"size":0}"""));
    }
}
