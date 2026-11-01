using BigFatFile.Contracts.Api;
using BigFatFile.Server.Auth;
using Microsoft.AspNetCore.Http.HttpResults;

namespace BigFatFile.Server.Api;

internal static class TenantEndpoints
{
    public static void MapTenantEndpoints(this IEndpointRouteBuilder tenant) =>
        tenant.MapGet("", GetTenantAsync)
            .WithName("getTenant")
            .WithSummary("Get the tenant")
            .WithDescription("""
                Returns the tenant's name, its plan and when it was created.
                """)
            .ProducesError(ApiErrors.NotFound)
            .RequireAuthorization(ScopeAuthorization.PolicyFor(BffScopes.MembersRead));

    private static Task<Results<Ok<Tenant>, ProblemHttpResult>> GetTenantAsync(Guid id, CancellationToken cancellationToken) =>
        throw new NotImplementedException();
}
