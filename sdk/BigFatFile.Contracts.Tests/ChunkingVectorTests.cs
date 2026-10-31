using System.Text;
using System.Text.Json;
using BigFatFile.Contracts.Chunking;
using BigFatFile.Contracts.Manifests;

namespace BigFatFile.Contracts.Tests;

public sealed class ChunkingVectorTests
{
    public static TheoryData<string> VectorNames => Vectors.Names("chunking.json");

    [Fact]
    public void ParametersMatchTheVectors()
    {
        var chunker = Vectors.Load("chunking.json").GetProperty("chunker");

        Assert.Equal(ChunkerParameters.Algorithm, chunker.GetProperty("algorithm").GetString());
        Assert.Equal(ChunkerParameters.MinSize, chunker.GetProperty("minSize").GetInt32());
        Assert.Equal(ChunkerParameters.AverageSize, chunker.GetProperty("averageSize").GetInt32());
        Assert.Equal(ChunkerParameters.MaxSize, chunker.GetProperty("maxSize").GetInt32());
        Assert.Equal(ChunkerParameters.NormalizationLevel, chunker.GetProperty("normalizationLevel").GetInt32());
    }

    [Theory]
    [MemberData(nameof(VectorNames))]
    public async Task ChunkReaderCutsAndHashesLikeTheReference(string name)
    {
        var vector = Vectors.Find("chunking.json", name);
        var input = Materialise(vector.GetProperty("input"));

        var actual = new List<(long Offset, int Size, string Hash)>();
        using (var reader = new ChunkReader(new MemoryStream(input)))
        {
            while (await reader.ReadNextAsync(TestContext.Current.CancellationToken) is { } chunk)
            {
                actual.Add((chunk.Offset, chunk.Size, chunk.Hash.ToString()));
            }

            Assert.Equal(vector.GetProperty("hash").GetString(), reader.FileHash.ToString());
            Assert.Equal(vector.GetProperty("size").GetInt64(), reader.Length);
        }

        var expected = vector.GetProperty("chunks").EnumerateArray()
            .Select(c => (c.GetProperty("offset").GetInt64(), c.GetProperty("size").GetInt32(), c.GetProperty("hash").GetString()!))
            .ToList();
        Assert.Equal(expected, actual);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(4093)]
    [InlineData(65536)]
    public async Task ShortReadsGiveTheSameManifest(int readSize)
    {
        var vector = Vectors.Find("chunking.json", "random-with-insert");
        var input = Materialise(vector.GetProperty("input"));
        if (readSize == 1)
        {
            // One byte per read over 64 MiB takes too long; a 9 MiB prefix covers the cases.
            input = input[..(9 * 1024 * 1024)];
        }

        var whole = await FileManifest.CreateAsync(new MemoryStream(input), TestContext.Current.CancellationToken);
        var trickled = await FileManifest.CreateAsync(
            new ShortReadStream(new MemoryStream(input), readSize), TestContext.Current.CancellationToken);

        Assert.Equal(whole.ToJson(), trickled.ToJson());
    }

    private static byte[] Materialise(JsonElement parts)
    {
        using var output = new MemoryStream();
        foreach (var part in parts.EnumerateArray())
        {
            var bytes = new byte[part.GetProperty("length").GetInt64()];
            if (part.GetProperty("kind").GetString() == "xof")
            {
                using var hasher = Blake3.Hasher.New();
                hasher.Update(Encoding.UTF8.GetBytes(part.GetProperty("seed").GetString()!));
                hasher.Finalize(part.GetProperty("offset").GetInt64(), bytes);
            }

            output.Write(bytes);
        }

        return output.ToArray();
    }

    private sealed class ShortReadStream(Stream inner, int readSize) : Stream
    {
        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }

        public override int Read(byte[] buffer, int offset, int count) =>
            inner.Read(buffer, offset, Math.Min(count, readSize));

        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) =>
            inner.ReadAsync(buffer[..Math.Min(buffer.Length, readSize)], cancellationToken);

        public override void Flush() { }
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}
