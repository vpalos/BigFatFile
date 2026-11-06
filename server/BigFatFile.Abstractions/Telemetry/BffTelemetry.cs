using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace BigFatFile.Abstractions.Telemetry;

/// <summary>
/// The one source of BigFatFile's own spans and the one meter of its own metrics.
/// Both core and modules use these, so all of BFF's signals can arrive in one stream.
/// </summary>
public static class BffTelemetry
{
    public const string Name = "BigFatFile";

    public static ActivitySource ActivitySource { get; } = new(Name);

    public static Meter Meter { get; } = new(Name);
}
