using BigFatFile.Contracts.Api;
using BigFatFile.Server.Api.Errors;
using BigFatFile.Server.Auth.Tokens;
using BigFatFile.Server.Database;
using BigFatFile.Server.Models;
using Microsoft.AspNetCore.Http.HttpResults;

namespace BigFatFile.Server.Api.Endpoints;

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

    private static async Task<Results<Ok<Tenant>, ProblemHttpResult>> GetTenantAsync(
        Guid id, RequestDatabase database, CancellationToken cancellationToken)
    {
        var connection = await database.OpenAsync(cancellationToken);
        if (await TenantModel.GetAsync(connection, id, cancellationToken) is not { } tenant)
        {
            return Problems.Of(ApiErrors.NotFound);
        }

        return TypedResults.Ok(new Tenant(tenant.Id, tenant.Name, "unlimited", tenant.CreatedAt));
    }
}
