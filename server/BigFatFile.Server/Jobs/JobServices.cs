using BigFatFile.Server.Database;
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
        // Hangfire uses the owner's data source: it creates its own tables, and jobs work
        // across tenants. Its connections get the same settings and tracing rules as the others.
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

    public static DashboardOptions DashboardOptions() => new()
    {
        Authorization = [new OperatorOnly()],
        AppPath = "/",
    };

    // Resolves the data source on first use. When the build runs the app to write the
    // OpenAPI document, the app also maps the dashboard, but there is no database then.
    private sealed class DataSourceConnectionFactory(IServiceProvider provider) : IConnectionFactory
    {
        public NpgsqlConnection GetOrCreateConnection() =>
            provider.GetRequiredKeyedService<NpgsqlDataSource>(DatabaseServices.Owner).CreateConnection();
    }

    private sealed class OperatorOnly : IDashboardAuthorizationFilter
    {
        public bool Authorize(DashboardContext context) => context.GetHttpContext().User.IsInRole(OperatorRole);
    }
}
