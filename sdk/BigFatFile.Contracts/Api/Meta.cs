namespace BigFatFile.Contracts.Api;

/// <summary>What a client needs to know about a server before it talks to it.</summary>
/// <param name="Features">The licensed features that are on.</param>
public sealed record Meta(
    string ApiVersion,
    string ServerVersion,
    MetaChunker Chunker,
    string NameKeyUnicodeVersion,
    IReadOnlyList<string> Features);

public sealed record MetaChunker(
    string Algorithm,
    int MinSize,
    int AverageSize,
    int MaxSize,
    int NormalizationLevel);
