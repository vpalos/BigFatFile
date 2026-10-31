using BigFatFile.Abstractions.Features;
using Microsoft.Extensions.Configuration;

namespace BigFatFile.Abstractions.Modules;

/// <summary>What a module can read while it registers its services.</summary>
public sealed class BffModuleContext(IConfiguration configuration, IFeatureGate features)
{
    public IConfiguration Configuration { get; } = configuration;

    /// <summary>A module checks its own feature key here and registers nothing when it is off.</summary>
    public IFeatureGate Features { get; } = features;
}
