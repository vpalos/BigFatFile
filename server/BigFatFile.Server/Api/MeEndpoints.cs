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
            .WithSummary("The signed-in user, their tenants and their role in each.")
            .Produces(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
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
            return TypedResults.Problem(
                statusCode: StatusCodes.Status403Forbidden,
                title: "This endpoint needs a signed-in user.",
                type: "https://bigfatfile.com/problems/user-required",
                extensions: new Dictionary<string, object?> { ["code"] = "user_required" });
        }

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var tenants = await connection.QueryAsync<MeWithinTenant>(new CommandDefinition(
            """
            SELECT t.id, t.name, m.role
            FROM tenant_members m
            JOIN tenants t ON t.id = m.tenant_id
            WHERE m.user_id = @UserId
            ORDER BY t.name
            """,
            new { UserId = user.Id },
            cancellationToken: cancellationToken));

        return TypedResults.Ok(new Me(user.Id, user.Email!, [.. tenants]));
    }
}
