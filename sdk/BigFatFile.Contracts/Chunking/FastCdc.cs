namespace BigFatFile.Contracts.Chunking;

/// <summary>
/// The FastCDC cut rule (Xia et al., 2020), as implemented by fastcdc-rs v2020 with
/// <see cref="ChunkerParameters"/>.
/// </summary>
public static class FastCdc
{
    /// <summary>
    /// Returns the length of the chunk that starts at the beginning of <paramref name="data"/>.
    /// </summary>
    /// <param name="data">
    /// The bytes from the chunk start: at least <see cref="ChunkerParameters.MaxSize"/> bytes,
    /// or everything up to the end of the file. Fewer bytes than that give a wrong cut.
    /// </param>
    public static int FindCut(ReadOnlySpan<byte> data)
    {
        var remaining = data.Length;
        if (remaining <= ChunkerParameters.MinSize)
        {
            return remaining;
        }

        var center = ChunkerParameters.AverageSize;
        if (remaining > ChunkerParameters.MaxSize)
        {
            remaining = ChunkerParameters.MaxSize;
        }
        else if (remaining < center)
        {
            center = remaining;
        }

        // The reference scans in byte pairs, so it never tests the last byte of an odd length.
        center &= ~1;
        var end = remaining & ~1;

        var gear = Gear.Table;
        ulong hash = 0;
        var i = ChunkerParameters.MinSize;

        for (; i < center; i++)
        {
            hash = (hash << 1) + gear[data[i]];
            if ((hash & ChunkerParameters.MaskSmall) == 0)
            {
                return i;
            }
        }

        for (; i < end; i++)
        {
            hash = (hash << 1) + gear[data[i]];
            if ((hash & ChunkerParameters.MaskLarge) == 0)
            {
                return i;
            }
        }

        return remaining;
    }
}
