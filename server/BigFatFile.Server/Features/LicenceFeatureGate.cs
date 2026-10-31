using BigFatFile.Abstractions.Features;

namespace BigFatFile.Server.Features;

/// <summary>
/// Answers from the licence. This build reads no licence, so every reserved key is off.
/// </summary>
internal sealed class LicenceFeatureGate : IFeatureGate
{
    public bool IsEnabled(string key) =>
        FeatureKeys.All.Contains(key)
            ? false
            : throw new ArgumentException($"'{key}' is not a feature key. The keys are listed in {nameof(FeatureKeys)}.", nameof(key));

    public IReadOnlyList<string> EnabledKeys => [.. FeatureKeys.All.Where(IsEnabled).Order(StringComparer.Ordinal)];
}
