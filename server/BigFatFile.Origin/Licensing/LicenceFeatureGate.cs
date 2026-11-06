using BigFatFile.Abstractions.Licensing;

namespace BigFatFile.Origin.Licensing;

/// <summary>
/// Answers what features are enabled, from the licence.
/// </summary>
internal sealed class LicenceFeatureGate : IFeatureGate
{
    public bool IsEnabled(string key) =>
        FeatureKeys.All.Contains(key)
            ? false
            : throw new ArgumentException($"'{key}' is not a feature key. The keys are listed in {nameof(FeatureKeys)}.", nameof(key));

    public IReadOnlyList<string> EnabledKeys => [.. FeatureKeys.All.Where(IsEnabled).Order(StringComparer.Ordinal)];
}
