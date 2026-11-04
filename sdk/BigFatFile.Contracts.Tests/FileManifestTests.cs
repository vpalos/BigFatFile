using BigFatFile.Contracts.Chunking;
using BigFatFile.Contracts.Hashing;
using BigFatFile.Contracts.Manifests;

namespace BigFatFile.Contracts.Tests;

public sealed class FileManifestTests
{
    private static readonly ContentHash SomeHash = ContentHash.Compute([1, 2, 3]);
    private static readonly Guid SomeObject = Guid.Parse("0192f3a4-5b6c-7d8e-9f01-23456789abcd");

    private static FileManifest Chunked(long size, params int[] chunkSizes) => new()
    {
        Chunking = ManifestChunking.Default,
        Chunks = [.. chunkSizes.Select(chunkSize => new ManifestChunk(SomeHash, chunkSize))],
        Size = size,
        Hash = SomeHash,
    };

    [Fact]
    public void JsonHasTheDocumentedShape()
    {
        Assert.Equal(
            $$"""{"chunking":{"algorithm":"fastcdc-2020","gear":1,"minSize":1048576,"averageSize":4194304,"maxSize":8388608,"normalizationLevel":1},"chunks":[{"hash":"{{SomeHash}}","size":3}],"size":3,"hash":"{{SomeHash}}"}""",
            Chunked(3, 3).ToJson());
    }

    [Fact]
    public void JsonOfAWholeObjectHasTheDocumentedShape()
    {
        var manifest = new FileManifest { Chunking = ManifestChunking.None, Object = SomeObject, Size = 3 };

        Assert.Equal($$"""{"chunking":{"algorithm":"none"},"object":"{{SomeObject}}","size":3}""", manifest.ToJson());
    }

    [Fact]
    public void JsonRoundTrips()
    {
        var manifest = Chunked(ChunkerParameters.MinSize + 7, ChunkerParameters.MinSize, 7);

        Assert.Equal(manifest.ToJson(), FileManifest.FromJson(manifest.ToJson()).ToJson());
    }

    [Fact]
    public void TheHashIsOptional()
    {
        var manifest = FileManifest.FromJson($$"""{"chunking":{"algorithm":"none"},"object":"{{SomeObject}}","size":0}""");

        Assert.Null(manifest.Hash);
        Assert.Null(manifest.FindError());
    }

    [Fact]
    public void MissingChunkingIsRejected()
    {
        Assert.ThrowsAny<System.Text.Json.JsonException>(() => FileManifest.FromJson("""{"chunks":[],"size":0}"""));
    }

    [Fact]
    public void AnEmptyFileHasNoChunks()
    {
        Assert.Null(Chunked(0).FindError());
    }

    [Fact]
    public void ChunksMustAddUpToTheSize()
    {
        Assert.NotNull(Chunked(4, 3).FindError());
    }

    [Fact]
    public void OnlyTheLastChunkMayBeSmallerThanTheMinimum()
    {
        Assert.NotNull(Chunked(ChunkerParameters.MinSize + 7, 7, ChunkerParameters.MinSize).FindError());
    }

    [Fact]
    public void NoChunkMayExceedTheMaximum()
    {
        var size = ChunkerParameters.MaxSize + 1;

        Assert.NotNull(Chunked(size, size).FindError());
    }

    [Fact]
    public void TheLimitsComeFromTheManifestsOwnChunking()
    {
        const int mib = 1024 * 1024;
        var smaller = ManifestChunking.Default with { MinSize = mib / 2, AverageSize = mib, MaxSize = 2 * mib };

        Assert.NotNull((Chunked(3 * mib, 3 * mib) with { Chunking = smaller }).FindError());
        Assert.Null((Chunked(mib, mib / 2, mib / 2) with { Chunking = smaller }).FindError());
    }

    [Fact]
    public void AnIncompleteChunkingIsRejected()
    {
        Assert.NotNull((Chunked(3, 3) with { Chunking = new ManifestChunking("fastcdc-2020") }).FindError());
    }

    [Fact]
    public void AWholeObjectHasNoChunks()
    {
        var manifest = new FileManifest { Chunking = ManifestChunking.None, Chunks = [], Object = SomeObject, Size = 0 };

        Assert.NotNull(manifest.FindError());
    }

    [Fact]
    public void AWholeObjectNamesItsObject()
    {
        Assert.NotNull(new FileManifest { Chunking = ManifestChunking.None, Size = 0 }.FindError());
    }

    [Fact]
    public void AChunkedManifestNamesNoObject()
    {
        Assert.NotNull((Chunked(3, 3) with { Object = SomeObject }).FindError());
    }
}
