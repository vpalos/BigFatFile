namespace BigFatFile.Contracts.Chunking;

/// <summary>
/// The parameters of the content-defined chunker in this library.
/// Each manifest records them, as <see cref="Manifests.ManifestChunking.Default"/>.
/// </summary>
public static class ChunkerParameters
{
    public const string Algorithm = "fastcdc-2020";

    /// <summary>
    /// The number of the GEAR table that the chunker uses. A different table cuts differently.
    /// </summary>
    public const int Gear = 1;

    public const int MinSize = 1 * 1024 * 1024;
    public const int AverageSize = 4 * 1024 * 1024;
    public const int MaxSize = 8 * 1024 * 1024;

    public const int NormalizationLevel = 1;

    // Entries 23 and 21 of the MASKS table in fastcdc-rs v2020:
    // - the masks it selects for a 4 MiB average at normalisation level 1;
    // - MaskSmall applies below the average size;
    // - MaskLarge above it.
    internal const ulong MaskSmall = 0x0000d93777537000;
    internal const ulong MaskLarge = 0x0000d91767537000;
}
