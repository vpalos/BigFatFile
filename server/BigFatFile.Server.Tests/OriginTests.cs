using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using BigFatFile.Contracts.Api;
using BigFatFile.Contracts.Chunking;
using BigFatFile.Server.Auth;
using BigFatFile.Server.Development;
using Dapper;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace BigFatFile.Server.Tests;

public sealed class OriginTests(Origin origin) : IClassFixture<Origin>
{
    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    [Fact]
    public async Task HealthIsOk()
    {
        using var client = origin.CreateClient();

        var response = await client.GetAsync("/health", Cancellation);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(Cancellation);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("healthy", body.GetProperty("status").GetString());
        var timestamp = body.GetProperty("timestamp").GetDateTimeOffset();
        Assert.Equal(TimeSpan.Zero, timestamp.Offset);
        Assert.InRange(DateTimeOffset.UtcNow - timestamp, TimeSpan.Zero, TimeSpan.FromMinutes(1));
    }

    [Fact]
    public async Task MetaReportsTheChunker()
    {
        using var client = origin.CreateClient();

        var meta = await client.GetFromJsonAsync<Meta>("/api/v1/meta", Cancellation);

        Assert.Equal(ApiVersion.Current, meta!.ApiVersion);
        Assert.Equal(ChunkerParameters.AverageSize, meta.Chunker.AverageSize);
        Assert.Empty(meta.Features);
    }

    [Fact]
    public async Task TheServedSpecIsTheCommittedSpec()
    {
        using var client = origin.CreateClient();

        var served = JsonNode.Parse(await client.GetStringAsync("/api/v1/openapi.json", Cancellation))!.AsObject();
        // At run time the server adds its own address, so that "try it" calls this instance.
        served.Remove("servers");
        var committed = JsonNode.Parse(await File.ReadAllTextAsync(Path.Combine(RepositoryRoot(), "api", "openapi.json"), Cancellation));

        var difference = FirstDifference(committed, served, "$");
        Assert.True(difference is null, $"api/openapi.json differs from what the server serves at {difference}. Build the server to regenerate it.");
    }

    [Fact]
    public async Task TheApiReferenceIsServed()
    {
        using var client = origin.CreateClient();

        var response = await client.GetAsync("/api/docs/", Cancellation);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task MetricsAreServed()
    {
        using var client = origin.CreateClient();
        await client.GetAsync("/api/v1/meta", Cancellation);

        var metrics = await client.GetStringAsync("/metrics", Cancellation);

        Assert.Contains("http_server_request_duration", metrics);
    }

    [Fact]
    public async Task TheCoreMigrationsAreRecorded()
    {
        await using var connection = new NpgsqlConnection(origin.ConnectionString);

        var files = await connection.QueryAsync<string>(
            "SELECT prefix || '_' || lpad(number::text, 4, '0') || '_' || name || '.sql' FROM schema_migrations ORDER BY prefix, number");

        Assert.Equal(["core_0001_auth.sql", "core_0002_tenants.sql", "core_0003_server_keys.sql"], files);
    }

    /// <summary>
    /// Selects every column the EF model maps. A package update that adds a column to the
    /// Identity or OpenIddict model fails here until a core_ migration adds it.
    /// </summary>
    [Fact]
    public async Task TheAuthTablesMatchTheEfModel()
    {
        await using var scope = origin.Factory.Services.CreateAsyncScope();
        var model = scope.ServiceProvider.GetRequiredService<AuthDbContext>().Model;
        await using var connection = new NpgsqlConnection(origin.ConnectionString);

        // Owned types are stored inside their owner's JSON column, which the owner's check covers.
        foreach (var entity in model.GetEntityTypes().Where(e => !e.IsOwned()))
        {
            var table = entity.GetTableName()!;
            var columns = entity.GetProperties().Select(p => p.GetColumnName()).ToList();
            await connection.ExecuteAsync($"SELECT {string.Join(", ", columns)} FROM {table} LIMIT 0");
        }
    }

    [Fact]
    public async Task TheJobDashboardIsForOperators()
    {
        using var anonymous = origin.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync("/admin/jobs", Cancellation)).StatusCode);

        using var browser = origin.CreateClient();
        await SignIn.SignInWithPasswordAsync(browser, DevSeed.UserEmail, DevSeed.UserPassword);
        Assert.Equal(HttpStatusCode.OK, (await browser.GetAsync("/admin/jobs", Cancellation)).StatusCode);
    }

    private static string? FirstDifference(JsonNode? expected, JsonNode? actual, string path)
    {
        if (expected is JsonObject expectedObject && actual is JsonObject actualObject)
        {
            foreach (var key in expectedObject.Select(p => p.Key).Union(actualObject.Select(p => p.Key)))
            {
                if (FirstDifference(expectedObject[key], actualObject[key], $"{path}.{key}") is { } found)
                {
                    return found;
                }
            }

            return null;
        }

        return JsonNode.DeepEquals(expected, actual) ? null : $"{path} (committed: {expected?.ToJsonString()}, served: {actual?.ToJsonString()})";
    }

    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "BigFatFile.slnx")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new InvalidOperationException("The repository root was not found.");
    }
}
