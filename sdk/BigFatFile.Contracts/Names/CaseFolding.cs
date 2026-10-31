namespace BigFatFile.Contracts.Names;

internal static partial class CaseFolding
{
    public static int Fold(int codePoint)
    {
        if (codePoint < 0x80)
        {
            return codePoint is >= 'A' and <= 'Z' ? codePoint + 32 : codePoint;
        }

        var index = Sources.BinarySearch(codePoint);
        return index >= 0 ? Targets[index] : codePoint;
    }
}
