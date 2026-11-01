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
            .ProducesError(ApiErrors.AuthenticationRequired)
            .ProducesError(ApiErrors.Forbidden);
        tenant.MapTenantEndpoints();
        tenant.MapMemberEndpoints();
        tenant.MapInvitationEndpoints();
    }
}
