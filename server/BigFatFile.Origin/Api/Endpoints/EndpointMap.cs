using BigFatFile.Origin.Api.Errors;
using BigFatFile.Origin.Health;
using BigFatFile.Origin.Jobs;
using BigFatFile.Origin.Modules;
using Hangfire;
using Scalar.AspNetCore;

namespace BigFatFile.Origin.Api.Endpoints;

/// <summary>Every route of the Origin. Each group is made here, so this file shows how the API spec is put together.</summary>
internal static class EndpointMap
{
    public static void MapEndpoints(this IEndpointRouteBuilder app, IReadOnlyList<LoadedModule> modules)
    {
        app.MapHealthChecks("/health", HealthServices.EndpointOptions);
        app.MapPrometheusScrapingEndpoint("/metrics");
        app.MapHangfireDashboard("/admin/jobs", JobServices.DashboardOptions());

        app.MapOpenApi(ApiServices.DocumentPath);
        app.MapScalarApiReference("/api/docs", options => options
            .WithTitle("BigFatFile API")
            .WithOpenApiRoutePattern(ApiServices.DocumentPath));

        app.MapGroup("/connect").ExcludeFromDescription().MapConnectEndpoints();
        app.MapGroup("/account").ExcludeFromDescription().MapSignInFormEndpoints();

        var api = app.MapGroup("/api/v1");
        api.MapGroup("/meta").WithTags("meta").MapMetaEndpoints();
        api.MapGroup("/account").WithTags("account").MapAccountEndpoints();

        var tenant = api.MapGroup("/tenants/{id:guid}")
            .WithTags("tenants")
            .ProducesError(ApiErrors.AuthenticationRequired)
            .ProducesError(ApiErrors.Forbidden)
            .RequireTenantAccess();
        tenant.MapTenantEndpoints();
        tenant.MapMemberEndpoints();
        tenant.MapInvitationEndpoints();

        foreach (var loaded in modules)
        {
            loaded.Module.MapEndpoints(app.MapGroup("/api/" + loaded.Module.Name));
        }
    }
}
