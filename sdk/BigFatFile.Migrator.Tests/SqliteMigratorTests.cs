using System.Data.Common;
using Dapper;
using Microsoft.Data.Sqlite;

namespace BigFatFile.Migrator.Tests;

public sealed class SqliteMigratorTests : MigratorContract, IDisposable
{
    private readonly string directory = Directory.CreateTempSubdirectory("bff-migrator-").FullName;

    protected override MigrationDialect Dialect => MigrationDialect.Sqlite;

    protected override async Task<DbConnection> OpenEmptyDatabaseAsync()
    {
        var connection = new SqliteConnection($"Data Source={Path.Combine(directory, "test.db")};Pooling=False");
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        return connection;
    }

    protected override DbConnection OpenSameDatabase(DbConnection connection) =>
        new SqliteConnection(connection.ConnectionString);

    [Fact]
    public async Task ANoTransactionFileCanVacuum()
    {
        await using var connection = await OpenEmptyDatabaseAsync();
        var files = new[] { Files.Create("sync_0001_vacuum.sql", "-- no-transaction\nVACUUM;") };

        var report = await new SchemaMigrator(Dialect).RunAsync(connection, files, TestContext.Current.CancellationToken);

        Assert.Single(report.Applied);
        Assert.Equal(1, await connection.ExecuteScalarAsync<long>("SELECT COUNT(*) FROM schema_migrations"));
    }

    public void Dispose() => Directory.Delete(directory, recursive: true);
}
