using System.Data.Common;
using BigFatFile.Abstractions.Licensing;

namespace BigFatFile.Origin.Licensing;

/// <summary>
/// The open core's implicit plan.
/// Used when no module registers a <see cref="IPlanPolicy"/>.
/// </summary>
internal sealed class UnlimitedPlanPolicy : IPlanPolicy
{
    public ValueTask<TenantPlan> GetPlanAsync(DbConnection connection, Guid tenantId, CancellationToken cancellationToken) =>
        ValueTask.FromResult(TenantPlan.Unlimited);
}
