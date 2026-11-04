using System.Text.Json.Serialization;
using BigFatFile.Contracts.Chunking;

namespace BigFatFile.Contracts.Manifests;

/// <summary>
/// The chunking that cut a file, recorded in its manifest: the algorithm and its parameters,
/// or <c>none</c> for a file that is stored as one whole object.
/// </summary>
/// <param name="Gear">The number of the GEAR table.</param>
/// <param name="MinSize">The smallest chunk, in bytes. Only the last chunk of a file may be smaller.</param>
/// <param name="AverageSize">The average chunk size the masks aim at, in bytes.</param>
/// <param name="MaxSize">The largest chunk, in bytes.</param>
/// <param name="NormalizationLevel">The normalisation level, which selects the boundary masks.</param>
public sealed record ManifestChunking(
    string Algorithm,
    int? Gear = null,
    int? MinSize = null,
    int? AverageSize = null,
    int? MaxSize = null,
    int? NormalizationLevel = null)
{
    public const string NoneAlgorithm = "none";

    /// <summary>The parameters of the chunker in this library.</summary>
    public static ManifestChunking Default { get; } = new(
        ChunkerParameters.Algorithm,
        ChunkerParameters.Gear,
        ChunkerParameters.MinSize,
        ChunkerParameters.AverageSize,
        ChunkerParameters.MaxSize,
        ChunkerParameters.NormalizationLevel);

    public static ManifestChunking None { get; } = new(NoneAlgorithm);

    /// <summary>
    /// The parameter sets that the Origin accepts in a manifest. It lists them in
    /// <c>GET /api/v1/meta</c>.
    /// </summary>
    public static IReadOnlyList<ManifestChunking> Accepted { get; } = [Default];

    [JsonIgnore]
    public bool IsNone => Algorithm == NoneAlgorithm;
}
