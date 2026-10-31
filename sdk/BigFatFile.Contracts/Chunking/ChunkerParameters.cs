namespace BigFatFile.Contracts.Chunking;

/// <summary>
/// The fixed parameters of the content-defined chunker.
/// Implementations must use exactly these values.
/// The Origin server reports them in <c>GET /api/v1/meta</c>.
/// </summary>
public static class ChunkerParameters
{
    public const string Algorithm = "fastcdc-2020";

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
