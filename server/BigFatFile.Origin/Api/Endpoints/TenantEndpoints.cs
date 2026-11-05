using BigFatFile.Contracts.Api;
using BigFatFile.Origin.Api.Errors;
using BigFatFile.Origin.Auth.Tokens;
using BigFatFile.Origin.Database;
using BigFatFile.Origin.Models;
using Microsoft.AspNetCore.Http.HttpResults;

namespace BigFatFile.Origin.Api.Endpoints;

internal static class TenantEndpoints
{
    public static void MapTenantEndpoints(this IEndpointRouteBuilder tenant) =>
        tenant.MapGet("", GetTenantAsync)
            .WithName("getTenant")
            .WithSummary("Get the tenant")
            .WithDescription("""
                Returns the tenant's name, its plan and when it was created.
                """)
            .ProducesError(ApiErrors.MemberRequired)
            .ProducesError(ApiErrors.NotFound)
            .RequireAuthorization(ScopeAuthorization.PolicyFor(BffScopes.MembersRead));

    private static async Task<Results<Ok<Tenant>, ProblemHttpResult>> GetTenantAsync(
        Guid id, ScopedDatabase database, CancellationToken cancellationToken)
    {
        var connection = await database.OpenAsync(cancellationToken);
        if (await TenantModel.GetAsync(connection, id, cancellationToken) is not { } tenant)
        {
            return Problems.Of(ApiErrors.NotFound);
        }

        return TypedResults.Ok(new Tenant(tenant.Id, tenant.Name, "unlimited", tenant.CreatedAt));
    }
}
