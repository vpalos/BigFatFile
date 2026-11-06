using BigFatFile.Origin.Api.Errors;
using BigFatFile.Origin.Jobs;
using BigFatFile.Origin.Modules;
using BigFatFile.Origin.Server.Health;
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

        var api = app.MapGroup("/api/v1").RequireCsrfToken();
        var groups = new HashSet<string>(StringComparer.Ordinal);

        // The name of each group under /api/v1 is the first segment of its paths, and also their tag.
        RouteGroupBuilder Group(string name) =>
            groups.Add(name)
                ? api.MapGroup("/" + name).WithTags(name)
                : throw new InvalidOperationException($"The group /api/v1/{name} is mapped twice. A module cannot have the name of a core group.");

        Group("meta").MapMetaEndpoints();
        Group("account").MapAccountEndpoints();

        var tenant = Group("tenants").MapGroup("/{id:guid}")
            .ProducesError(ApiErrors.AuthenticationRequired)
            .ProducesError(ApiErrors.Forbidden)
            .RequireTenantAccess();
        tenant.MapTenantEndpoints();
        tenant.MapMemberEndpoints();
        tenant.MapInvitationEndpoints();
        tenant.MapAuditEndpoints();

        foreach (var loaded in modules)
        {
            loaded.Module.MapEndpoints(Group(loaded.Module.Name));
        }
    }
}
