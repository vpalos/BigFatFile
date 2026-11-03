using System.Diagnostics;
using BigFatFile.Origin.Server;
using Npgsql;

namespace BigFatFile.Origin.Database;

internal static class DatabaseServices
{
    /// <summary>
    /// The key of the owner's data source, for migrations and the job queue.
    /// Requests use the unkeyed one.
    /// </summary>
    public const string Owner = "owner";

    public static IServiceCollection AddDatabase(this IServiceCollection services) =>
        services
            .AddSingleton(provider => Build(provider, provider.GetRequiredService<OriginSettings>().Database, "bigfatfile"))
            .AddKeyedSingleton(Owner, (provider, _) => Build(provider, provider.GetRequiredService<OriginSettings>().DatabaseOwner, "bigfatfile-owner"))
            .AddHttpContextAccessor()
            .AddScoped<RequestDatabase>();

    private static NpgsqlDataSource Build(IServiceProvider provider, string connectionString, string name)
    {
        var builder = new NpgsqlDataSourceBuilder(connectionString) { Name = name };
        builder.UseLoggerFactory(provider.GetRequiredService<ILoggerFactory>());

        // The image has no Kerberos library, so asking for GSS encryption only prints an
        // error at every start. A connection string that names the setting keeps its value.
        if (!connectionString.Contains("gss", StringComparison.OrdinalIgnoreCase))
        {
            builder.ConnectionStringBuilder.GssEncryptionMode = GssEncryptionMode.Disable;
        }

        // Record a database span only inside a recorded request span or job span. The
        // other database calls are queue polling, health probes and the pool opening
        // connections; their spans would hide the useful traces.
        builder.ConfigureTracing(tracing => tracing
            .ConfigureCommandFilter(_ => Activity.Current is { Recorded: true })
            .ConfigureBatchFilter(_ => Activity.Current is { Recorded: true })
            .EnablePhysicalOpenTracing(false));

        return builder.Build();
    }
}
