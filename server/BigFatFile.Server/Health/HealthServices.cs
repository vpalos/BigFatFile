using System.Text.Json;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Npgsql;

namespace BigFatFile.Server.Health;

internal static class HealthServices
{
    public static IServiceCollection AddHealth(this IServiceCollection services)
    {
        services.AddHealthChecks().AddCheck<DatabaseHealthCheck>("database");
        return services;
    }

    /// <summary>
    /// <c>GET /health</c>: <c>{"status": "healthy", "timestamp": "..."}</c>, with 503 when a
    /// check fails. Which check failed goes to the log, not to the response.
    /// </summary>
    public static void MapHealth(this IEndpointRouteBuilder endpoints) =>
        endpoints.MapHealthChecks("/health", new HealthCheckOptions { ResponseWriter = WriteResponseAsync });

    private static async Task WriteResponseAsync(HttpContext context, HealthReport report)
    {
        context.Response.ContentType = "application/json; charset=utf-8";
        await using var json = new Utf8JsonWriter(context.Response.Body);
        json.WriteStartObject();
        json.WriteString("status", report.Status.ToString().ToLowerInvariant());
        json.WriteString("timestamp", DateTime.UtcNow);
        json.WriteEndObject();
        await json.FlushAsync(context.RequestAborted);
    }

    private sealed class DatabaseHealthCheck(NpgsqlDataSource dataSource) : IHealthCheck
    {
        public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
        {
            try
            {
                await using var command = dataSource.CreateCommand("SELECT 1");
                await command.ExecuteScalarAsync(cancellationToken);
                return HealthCheckResult.Healthy();
            }
            catch (Exception exception) when (exception is NpgsqlException or TimeoutException)
            {
                return HealthCheckResult.Unhealthy("The database does not answer.", exception);
            }
        }
    }
}
