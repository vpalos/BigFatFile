using System.Text;

namespace BigFatFile.Contracts.Names;

public static class NodeNames
{
    /// <summary>The Unicode version of the case folding table.</summary>
    public const string UnicodeVersion = CaseFolding.UnicodeVersion;

    /// <summary>
    /// The name key: two siblings may not have names with the same key. It is the name in
    /// NFC, simple-case-folded with the Unicode table in <see cref="CaseFolding"/>, then in
    /// NFC again.
    /// </summary>
    /// <exception cref="ArgumentException">The name contains an unpaired surrogate.</exception>
    public static string Key(string name)
    {
        if (!IsWellFormed(name))
        {
            throw new ArgumentException("The name contains an unpaired surrogate.", nameof(name));
        }

        var normalized = name.Normalize(NormalizationForm.FormC);
        var folded = new StringBuilder(normalized.Length);
        foreach (var rune in normalized.EnumerateRunes())
        {
            folded.Append(new Rune(CaseFolding.Fold(rune.Value)));
        }

        return folded.ToString().Normalize(NormalizationForm.FormC);
    }

    private static bool IsWellFormed(string text)
    {
        for (var i = 0; i < text.Length; i++)
        {
            if (char.IsHighSurrogate(text[i]) && i + 1 < text.Length && char.IsLowSurrogate(text[i + 1]))
            {
                i++;
            }
            else if (char.IsSurrogate(text[i]))
            {
                return false;
            }
        }

        return true;
    }
}
