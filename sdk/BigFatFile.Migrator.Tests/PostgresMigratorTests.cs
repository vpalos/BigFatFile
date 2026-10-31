using System.Data.Common;
using Dapper;
using Npgsql;
using Testcontainers.PostgreSql;

namespace BigFatFile.Migrator.Tests;

public sealed class PostgresFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer container = new PostgreSqlBuilder("postgres:18-alpine").Build();

    public async Task<string> CreateDatabaseAsync()
    {
        var name = "test_" + Guid.NewGuid().ToString("N");
        await using var admin = new NpgsqlConnection(container.GetConnectionString());
        await admin.ExecuteAsync($"CREATE DATABASE {name}");
        return new NpgsqlConnectionStringBuilder(container.GetConnectionString()) { Database = name }.ConnectionString;
    }

    public async ValueTask InitializeAsync() => await container.StartAsync();

    public async ValueTask DisposeAsync() => await container.DisposeAsync();
}

public sealed class PostgresMigratorTests(PostgresFixture postgres) : MigratorContract, IClassFixture<PostgresFixture>
{
    // Npgsql removes the password from ConnectionString once a connection opens.
    private string connectionString = "";

    protected override MigrationDialect Dialect => MigrationDialect.Postgres;

    protected override async Task<DbConnection> OpenEmptyDatabaseAsync()
    {
        connectionString = await postgres.CreateDatabaseAsync();
        var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        return connection;
    }

    protected override DbConnection OpenSameDatabase(DbConnection connection) => new NpgsqlConnection(connectionString);

    [Fact]
    public async Task ANoTransactionFileCanCreateAnIndexConcurrently()
    {
        await using var connection = await OpenEmptyDatabaseAsync();
        var files = new[]
        {
            Files.Table("core_0001_items.sql", "items"),
            Files.Create("core_0002_index.sql", "-- no-transaction\nCREATE INDEX CONCURRENTLY items_id ON items (id);"),
        };

        var report = await new SchemaMigrator(Dialect).RunAsync(connection, files, TestContext.Current.CancellationToken);

        Assert.Equal(2, report.Applied.Count);
    }

    [Fact]
    public async Task TheLockIsReleasedAfterTheRun()
    {
        await using var connection = await OpenEmptyDatabaseAsync();
        await new SchemaMigrator(Dialect).RunAsync(connection, [Files.Table("core_0001_items.sql", "items")], TestContext.Current.CancellationToken);

        await using var other = new NpgsqlConnection(connectionString);
        Assert.True(await other.ExecuteScalarAsync<bool>("SELECT pg_try_advisory_lock(x'4246465F4D494752'::bigint)"));
    }
}
