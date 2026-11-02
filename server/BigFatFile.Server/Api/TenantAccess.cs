using BigFatFile.Server.Api.Errors;
using BigFatFile.Server.Auth;
using BigFatFile.Server.Database;
using BigFatFile.Server.Models;

namespace BigFatFile.Server.Api;

/// <summary>
/// Lets a request into the <c>/tenants/{id}</c> endpoints only for the tenant that its session
/// or token works in, and only while the user is still a member of it. Any other request gets
/// 404, the same answer as for a tenant that does not exist.
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
            || !await IsMemberAsync(http, tenantId, userId))
        {
            return Problems.Of(ApiErrors.NotFound);
        }

        return await next(context);
    }

    private static async Task<bool> IsMemberAsync(HttpContext http, Guid tenantId, Guid userId)
    {
        var connection = await http.RequestServices.GetRequiredService<RequestDatabase>().OpenAsync(http.RequestAborted);
        return await MemberModel.ExistsAsync(connection, tenantId, userId, http.RequestAborted);
    }
}
