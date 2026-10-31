using System.Text;
using BigFatFile.Contracts.Hashing;

namespace BigFatFile.Contracts.Tests;

public sealed class ContentHashTests
{
    // The example hash from the Blake3.NET documentation.
    private const string Blake3OfBlake3 = "blake3:f890484173e516bfd935ef3d22b912dc9738de38743993cfedf2c9473b3216a4";

    [Fact]
    public void ComputeGivesTheKnownHash()
    {
        Assert.Equal(Blake3OfBlake3, ContentHash.Compute(Encoding.UTF8.GetBytes("BLAKE3")).ToString());
    }

    [Fact]
    public void ParseRoundTrips()
    {
        var hash = ContentHash.Parse(Blake3OfBlake3);

        Assert.Equal(Blake3OfBlake3, hash.ToString());
        Assert.Equal(ContentHash.Compute(Encoding.UTF8.GetBytes("BLAKE3")), hash);
    }

    [Theory]
    [InlineData("blake3:F890484173E516BFD935EF3D22B912DC9738DE38743993CFEDF2C9473B3216A4")]
    [InlineData("sha256:f890484173e516bfd935ef3d22b912dc9738de38743993cfedf2c9473b3216a4")]
    [InlineData("blake3:f890484173e516bfd935ef3d22b912dc9738de38743993cfedf2c9473b3216")]
    [InlineData("blake3:f890484173e516bfd935ef3d22b912dc9738de38743993cfedf2c9473b3216a4ff")]
    [InlineData("blake3:g890484173e516bfd935ef3d22b912dc9738de38743993cfedf2c9473b3216a4")]
    [InlineData("")]
    public void ParseRejectsAnythingButTheCanonicalForm(string text)
    {
        Assert.False(ContentHash.TryParse(text, out _));
    }
}
