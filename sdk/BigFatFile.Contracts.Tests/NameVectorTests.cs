using BigFatFile.Contracts.Names;

namespace BigFatFile.Contracts.Tests;

public sealed class NameVectorTests
{
    public static TheoryData<string> VectorNames => Vectors.Names("names.json");

    [Theory]
    [MemberData(nameof(VectorNames))]
    public void KeyMatchesTheVector(string name)
    {
        var vector = Vectors.Find("names.json", name);

        Assert.Equal(vector.GetProperty("key").GetString(), NodeNames.Key(vector.GetProperty("input").GetString()!));
    }

    [Fact]
    public void UnicodeVersionMatchesTheVectors()
    {
        Assert.Equal(NodeNames.UnicodeVersion, Vectors.Load("names.json").GetProperty("unicodeVersion").GetString());
    }

    // Built in code: attribute arguments are stored as UTF-8, which turns a lone surrogate into U+FFFD.
    public static TheoryData<string> UnpairedSurrogates => ["a\ud800b", "a\udc00", "\ud800", "\udc00\ud800"];

    [Theory]
    [MemberData(nameof(UnpairedSurrogates))]
    public void UnpairedSurrogatesAreRejected(string input)
    {
        Assert.Throws<ArgumentException>(() => NodeNames.Key(input));
    }
}
