using System.Data.Common;

namespace BigFatFile.Abstractions.Licensing;

/// <summary>
/// Returns the selected plan for each tenant.
/// The open core assigns <see cref="TenantPlan.Unlimited"/> to every tenant.
/// A module can replace this, by registering its own policy implementation.
/// No more than one single module is allowed to override the core policy
/// (this is usually `platform.billing`), otherwise the Origin refuses to start.
/// </summary>
public interface IPlanPolicy
{
    /// <param name="connection">
    /// The request's connection, with the tenant already set for row-level security.
    /// Run every query on it: a query on another connection would run without the tenant.
    /// </param>
    ValueTask<TenantPlan> GetPlanAsync(DbConnection connection, Guid tenantId, CancellationToken cancellationToken);
}
