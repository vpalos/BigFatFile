using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using BigFatFile.Contracts.Account;
using BigFatFile.Contracts.Api;
using BigFatFile.Contracts.Chunking;
using BigFatFile.Origin.Auth.Users;
using BigFatFile.Origin.Development;
using Dapper;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace BigFatFile.Origin.Tests;

public sealed class OriginTests(Origin origin, Postgres postgres) : IClassFixture<Origin>
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
    public async Task MetaReportsTheAccountOptions()
    {
        using var client = origin.CreateClient();

        var meta = await client.GetFromJsonAsync<Meta>("/api/v1/meta", Cancellation);

        Assert.Equal(SignUpPolicy.Invitation, meta!.Account.SignUp);
        Assert.True(meta.Account.Password);
        Assert.Empty(meta.Account.Providers);
    }

    [Fact]
    public async Task EveryPathOfTheSpecIsUnderApiV1()
    {
        using var client = origin.CreateClient();
        var spec = JsonNode.Parse(await client.GetStringAsync("/api/v1/openapi.json", Cancellation))!;

        var outside = spec["paths"]!.AsObject().Select(path => path.Key).Where(path => !path.StartsWith("/api/v1/")).ToList();

        Assert.Empty(outside);
    }

    [Fact]
    public async Task EveryTagIsTheFirstSegmentOfItsPath()
    {
        using var client = origin.CreateClient();
        var spec = JsonNode.Parse(await client.GetStringAsync("/api/v1/openapi.json", Cancellation))!;
        string[] methods = ["get", "put", "post", "delete", "options", "head", "patch", "trace"];

        var wrong = new List<string>();
        foreach (var (path, operations) in spec["paths"]!.AsObject().Where(path => path.Key.StartsWith("/api/v1/")))
        {
            var segment = path["/api/v1/".Length..].Split('/')[0];
            foreach (var (method, operation) in operations!.AsObject().Where(entry => methods.Contains(entry.Key)))
            {
                string[] tags = [.. operation!["tags"]?.AsArray().Select(tag => tag!.GetValue<string>()) ?? []];
                if (tags is not [var tag] || tag != segment)
                {
                    wrong.Add($"{method.ToUpperInvariant()} {path}: [{string.Join(", ", tags)}], expected [{segment}]");
                }
            }
        }

        Assert.Empty(wrong);
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
            "SELECT name FROM schema_migrations_core WHERE type = 0 ORDER BY id");

        Assert.Equal(["V0001__auth.sql", "V0002__tenants.sql", "V0003__server_keys.sql", "V0004__tenant_isolation.sql"], files);
    }

    /// <summary>
    /// Builds a second database from the EF model, and compares its auth tables with the ones
    /// that the core scripts made. After a package update changes the Identity, OpenIddict
    /// or Data Protection model, this test fails until a core script makes the same change.
    /// </summary>
    [Fact]
    public async Task TheAuthTablesMatchTheEfModel()
    {
        await using var scope = origin.Factory.Services.CreateAsyncScope();
        var auth = scope.ServiceProvider.GetRequiredService<AuthDbContext>();
        string[] tables = [.. auth.Model.GetEntityTypes().Select(e => e.GetTableName()!).Distinct()];

        var fromModel = await postgres.CreateDatabaseAsync();
        await using (var connection = new NpgsqlConnection(fromModel))
        {
            await connection.ExecuteAsync(auth.Database.GenerateCreateScript());
        }

        var expected = await SchemaOfAsync(fromModel, tables);
        var actual = await SchemaOfAsync(origin.ConnectionString, tables);
        var differences = expected.Except(actual).Select(line => "only in the EF model: " + line)
            .Concat(actual.Except(expected).Select(line => "only in the migrations: " + line))
            .Order()
            .ToList();
        Assert.True(differences.Count == 0, "The auth tables differ from the EF model:\n" + string.Join("\n", differences));
    }

    // One line per column, index and constraint, so that two databases compare as two sets of lines.
    private static async Task<HashSet<string>> SchemaOfAsync(string connectionString, string[] tables)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        var lines = await connection.QueryAsync<string>(
            """
            SELECT format('column %s.%s %s%s%s%s%s', table_name, column_name, data_type,
                          '(' || character_maximum_length || ')',
                          CASE WHEN is_nullable = 'NO' THEN ' not null' END,
                          ' default ' || column_default,
                          CASE WHEN is_identity = 'YES' THEN ' identity' END)
            FROM information_schema.columns
            WHERE table_schema = 'public' AND table_name = ANY(@tables)
            UNION ALL
            SELECT format('index %s', indexdef)
            FROM pg_indexes
            WHERE schemaname = 'public' AND tablename = ANY(@tables)
            UNION ALL
            SELECT format('constraint %s.%s %s', t.relname, c.conname, pg_get_constraintdef(c.oid))
            FROM pg_constraint c
            JOIN pg_class t ON t.oid = c.conrelid
            JOIN pg_namespace n ON n.oid = t.relnamespace
            WHERE n.nspname = 'public' AND t.relname = ANY(@tables)
            """,
            new { tables });
        return [.. lines];
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
