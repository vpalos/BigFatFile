using Hangfire;
using Hangfire.Dashboard;
using Hangfire.PostgreSql;
using Npgsql;

namespace BigFatFile.Server.Jobs;

internal static class JobServices
{
    public const string OperatorRole = "operator";

    public static IServiceCollection AddJobs(this IServiceCollection services, bool runServer)
    {
        // Hangfire uses the server's data source, so its connections get the same settings
        // and the same tracing rules.
        services.AddHangfire((provider, configuration) => configuration
            .UseSimpleAssemblyNameTypeSerializer()
            .UseRecommendedSerializerSettings()
            .UsePostgreSqlStorage(
                options => options.UseConnectionFactory(new DataSourceConnectionFactory(provider)),
                new PostgreSqlStorageOptions { SchemaName = "hangfire", PrepareSchemaIfNecessary = true }));

        if (runServer)
        {
            services.AddHangfireServer();
        }

        return services;
    }

    public static void MapJobDashboard(this IEndpointRouteBuilder endpoints) =>
        endpoints.MapHangfireDashboard("/admin/jobs", new DashboardOptions
        {
            Authorization = [new OperatorOnly()],
            AppPath = "/",
        });

    // Resolves the data source on first use: the build maps the dashboard to write the
    // OpenAPI document, and has no database to point a data source at.
    private sealed class DataSourceConnectionFactory(IServiceProvider provider) : IConnectionFactory
    {
        public NpgsqlConnection GetOrCreateConnection() => provider.GetRequiredService<NpgsqlDataSource>().CreateConnection();
    }

    private sealed class OperatorOnly : IDashboardAuthorizationFilter
    {
        public bool Authorize(DashboardContext context) => context.GetHttpContext().User.IsInRole(OperatorRole);
    }
}
