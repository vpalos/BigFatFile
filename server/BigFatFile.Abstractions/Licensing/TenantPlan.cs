namespace BigFatFile.Abstractions.Licensing;

/// <summary>
/// The definition of a plan, and the limits that come with it.
/// </summary>
public sealed record TenantPlan
{
    /// <summary>
    /// The plan of the open core, with no limits.
    /// </summary>
    public static TenantPlan Unlimited { get; } = new() { Key = "unlimited" };

    /// <summary>
    /// The plan's key, which the API returns as the tenant's plan.
    /// </summary>
    public required string Key { get; init; }
}
