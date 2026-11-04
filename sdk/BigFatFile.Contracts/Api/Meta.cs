using BigFatFile.Contracts.Account;
using BigFatFile.Contracts.Manifests;

namespace BigFatFile.Contracts.Api;

/// <summary>What a client needs to know about a server before it talks to it.</summary>
/// <param name="Chunkings">The chunking parameter sets that this server accepts in a manifest.</param>
/// <param name="Features">The licensed features that are on.</param>
/// <param name="Account">What the sign-in and sign-up flows offer on this server.</param>
public sealed record Meta(
    string ApiVersion,
    string ServerVersion,
    IReadOnlyList<ManifestChunking> Chunkings,
    string NameKeyUnicodeVersion,
    IReadOnlyList<string> Features,
    AccountOptions Account);
