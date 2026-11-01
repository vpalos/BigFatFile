using System.Security.Claims;
using BigFatFile.Contracts.Api;
using BigFatFile.Server.Auth;
using Dapper;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Identity;
using Npgsql;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace BigFatFile.Server.Api;

internal static class MeEndpoints
{
    public static void MapMeEndpoints(this IEndpointRouteBuilder api) =>
        api.MapGet("/me", GetMeAsync)
            .WithTags("tenant")
            .WithName("getMe")
            .WithSummary("Get the signed-in user")
            .WithDescription("""
                Returns the user that the token or session belongs to, and:

                - `tenantId`: the tenant that the token or session works in;
                - `tenants`: every tenant the user is a member of, with the user's role in each.
                """)
            .ProducesError(ApiErrors.AuthenticationRequired)
            .ProducesError(ApiErrors.Forbidden)
            .ProducesError(ApiErrors.UserRequired)
            .RequireAuthorization(ScopeAuthorization.PolicyFor(BffScopes.OpenId));

    private static async Task<Results<Ok<Me>, ProblemHttpResult>> GetMeAsync(
        ClaimsPrincipal principal, UserManager<BffUser> users, NpgsqlDataSource dataSource, CancellationToken cancellationToken)
    {
        // A client-credentials token belongs to an App, not to a user.
        var user = Guid.TryParse(principal.FindFirstValue(Claims.Subject), out var userId)
            ? await users.FindByIdAsync(userId.ToString())
            : null;
        if (user is null)
        {
            return Problems.Of(ApiErrors.UserRequired);
        }

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var tenants = await connection.QueryAsync<(Guid Id, string Name, string Role)>(new CommandDefinition(
            """
            SELECT t.id, t.name, m.role
            FROM tenant_members m
            JOIN tenants t ON t.id = m.tenant_id
            WHERE m.user_id = @UserId
            ORDER BY t.name
            """,
            new { UserId = user.Id },
            cancellationToken: cancellationToken));

        return TypedResults.Ok(new Me(
            user.Id,
            user.Email!,
            user.UserName!,
            null,
            [.. tenants.Select(t => new MeWithinTenant(t.Id, t.Name, Enum.Parse<TenantRole>(t.Role, ignoreCase: true)))]));
    }
}
