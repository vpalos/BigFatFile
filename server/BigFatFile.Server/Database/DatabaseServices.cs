using System.Diagnostics;
using BigFatFile.Server.Configuration;
using Npgsql;

namespace BigFatFile.Server.Database;

internal static class DatabaseServices
{
    public static IServiceCollection AddDatabase(this IServiceCollection services) =>
        services.AddSingleton(provider =>
        {
            var settings = provider.GetRequiredService<OriginSettings>();
            var builder = new NpgsqlDataSourceBuilder(settings.Database) { Name = "bigfatfile" };
            builder.UseLoggerFactory(provider.GetRequiredService<ILoggerFactory>());

            // The image has no Kerberos library, so asking for GSS encryption only prints an
            // error at every start. A connection string that names the setting keeps its value.
            if (!settings.Database.Contains("gss", StringComparison.OrdinalIgnoreCase))
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
        });
}
