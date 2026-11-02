using BigFatFile.Contracts.Account;

namespace BigFatFile.Contracts.Api;

/// <summary>What a client needs to know about a server before it talks to it.</summary>
/// <param name="Features">The licensed features that are on.</param>
/// <param name="Account">What the sign-in and sign-up flows offer on this server.</param>
public sealed record Meta(
    string ApiVersion,
    string ServerVersion,
    MetaChunker Chunker,
    string NameKeyUnicodeVersion,
    IReadOnlyList<string> Features,
    AccountOptions Account);

public sealed record MetaChunker(
    string Algorithm,
    int MinSize,
    int AverageSize,
    int MaxSize,
    int NormalizationLevel);
