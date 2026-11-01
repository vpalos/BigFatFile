using BigFatFile.Contracts.Api;
using BigFatFile.Server.Auth;
using Microsoft.AspNetCore.Http.HttpResults;

namespace BigFatFile.Server.Api;

internal static class TenantEndpoints
{
    public static void MapTenantEndpoints(this IEndpointRouteBuilder tenant) =>
        tenant.MapGet("", GetTenantAsync)
            .WithName("getTenant")
            .WithSummary("The tenant's name and plan.")
            .ProducesProblem(StatusCodes.Status404NotFound)
            .RequireAuthorization(ScopeAuthorization.PolicyFor(BffScopes.MembersRead));

    private static Task<Results<Ok<Tenant>, ProblemHttpResult>> GetTenantAsync(Guid id, CancellationToken cancellationToken) =>
        throw new NotImplementedException();
}
