namespace BigFatFile.Abstractions.Features;

/// <summary>
/// The one place that says whether a licensed feature is on.
/// - The core uses it to show or hide UI and to refuse endpoints;
/// - Modules use this to accept or refuse to activate.
/// </summary>
public interface IFeatureGate
{
    /// <param name="key">One of <see cref="FeatureKeys"/>.</param>
    /// <exception cref="ArgumentException">The key is not one of <see cref="FeatureKeys"/>.</exception>
    bool IsEnabled(string key);
}
