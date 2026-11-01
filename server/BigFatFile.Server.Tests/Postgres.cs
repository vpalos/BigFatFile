using Dapper;
using Npgsql;
using Testcontainers.PostgreSql;

[assembly: AssemblyFixture(typeof(BigFatFile.Server.Tests.Postgres))]

namespace BigFatFile.Server.Tests;

/// <summary>One PostgreSQL container for the test run; each origin gets its own database.</summary>
public sealed class Postgres : IAsyncLifetime
{
    // Each Origin's job server keeps about 20 connections open, and the test classes run
    // their Origins at the same time, so the default limit of 100 is not enough.
    private readonly PostgreSqlContainer container = new PostgreSqlBuilder("postgres:18-alpine")
        .WithCommand("-c", "max_connections=500")
        .Build();

    public async Task<string> CreateDatabaseAsync()
    {
        var name = "origin_" + Guid.NewGuid().ToString("N");
        await using var admin = new NpgsqlConnection(container.GetConnectionString());
        await admin.ExecuteAsync($"CREATE DATABASE {name}");
        return new NpgsqlConnectionStringBuilder(container.GetConnectionString()) { Database = name }.ConnectionString;
    }

    public async ValueTask InitializeAsync() => await container.StartAsync();

    public async ValueTask DisposeAsync() => await container.DisposeAsync();
}
