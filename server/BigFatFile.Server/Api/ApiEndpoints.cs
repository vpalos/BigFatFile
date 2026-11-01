using Microsoft.AspNetCore.Http.Metadata;
using Microsoft.AspNetCore.Mvc;

namespace BigFatFile.Server.Api;

internal static class ApiEndpoints
{
    public static void MapApiV1(this IEndpointRouteBuilder endpoints)
    {
        var api = endpoints.MapGroup("/api/v1");
        api.MapMetaEndpoints();
        api.MapMeEndpoints();

        var tenant = api.MapGroup("/tenants/{id:guid}")
            .WithTags("tenant")
            .WithMetadata(
                new ProducesResponseTypeMetadata(StatusCodes.Status401Unauthorized, typeof(void)),
                new ProducesResponseTypeMetadata(StatusCodes.Status403Forbidden, typeof(ProblemDetails), ["application/problem+json"]));
        tenant.MapTenantEndpoints();
        tenant.MapMemberEndpoints();
        tenant.MapInvitationEndpoints();
    }
}
