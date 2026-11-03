using BigFatFile.Contracts.Api;
using BigFatFile.Origin.Api.Errors;
using BigFatFile.Origin.Auth;
using BigFatFile.Origin.Auth.Tokens;
using BigFatFile.Origin.Database;
using BigFatFile.Origin.Models;
using Microsoft.AspNetCore.Authorization;

namespace BigFatFile.Origin.Api;

/// <summary>
/// Lets a request into the <c>/tenants/{id}</c> endpoints only for the tenant that its session
/// or token works in, and only while the user is still a member of it. Any other request gets
/// 404, the same answer as for a tenant that does not exist. An endpoint that needs one of
/// <see cref="BffScopes.AdminOnly"/> also needs the user to be an admin of the tenant.
/// </summary>
internal static class TenantAccess
{
    public static RouteGroupBuilder RequireTenantAccess(this RouteGroupBuilder group) =>
        group.AddEndpointFilter(CheckAsync);

    private static async ValueTask<object?> CheckAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var http = context.HttpContext;
        if (TenantClaims.TenantOf(http.User) is not { } tenantId
            || TenantClaims.UserOf(http.User) is not { } userId
            || !Guid.TryParse(http.Request.RouteValues["id"]?.ToString(), out var routeId)
            || routeId != tenantId
            || await RoleOfAsync(http, tenantId, userId) is not { } role)
        {
            return Problems.Of(ApiErrors.NotFound);
        }

        if (role != TenantRole.Admin && NeedsAdmin(http))
        {
            return Problems.Of(ApiErrors.AdminRequired);
        }

        return await next(context);
    }

    private static async Task<TenantRole?> RoleOfAsync(HttpContext http, Guid tenantId, Guid userId)
    {
        var connection = await http.RequestServices.GetRequiredService<RequestDatabase>().OpenAsync(http.RequestAborted);
        return await MemberModel.RoleOfAsync(connection, tenantId, userId, http.RequestAborted);
    }

    private static bool NeedsAdmin(HttpContext http) =>
        http.GetEndpoint()?.Metadata.GetOrderedMetadata<IAuthorizeData>()
            .Any(data => ScopeAuthorization.ScopeOf(data.Policy) is { } scope && BffScopes.AdminOnly.Contains(scope)) == true;
}
