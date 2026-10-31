using System.Data.Common;
using Dapper;

namespace BigFatFile.Migrator.Tests;

/// <summary>
/// The behaviour every dialect must have. Each test gets an empty database.
/// </summary>
public abstract class MigratorContract
{
    protected abstract MigrationDialect Dialect { get; }

    protected abstract Task<DbConnection> OpenEmptyDatabaseAsync();

    protected abstract DbConnection OpenSameDatabase(DbConnection connection);

    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    [Fact]
    public async Task AppliesPendingFilesInOrderAndRecordsThem()
    {
        await using var connection = await OpenEmptyDatabaseAsync();
        var files = new[]
        {
            Files.Create("core_0002_rows.sql", "INSERT INTO items (id) VALUES (1);"),
            Files.Table("core_0001_items.sql", "items"),
        };

        var report = await new SchemaMigrator(Dialect).RunAsync(connection, files, Cancellation);

        Assert.Equal(["core_0001_items.sql", "core_0002_rows.sql"], report.Applied.Select(f => f.FileName));
        Assert.Equal(1, await connection.ExecuteScalarAsync<long>("SELECT COUNT(*) FROM items"));
        Assert.Equal(2, await connection.ExecuteScalarAsync<long>("SELECT COUNT(*) FROM schema_migrations"));
    }

    [Fact]
    public async Task ASecondRunAppliesNothing()
    {
        await using var connection = await OpenEmptyDatabaseAsync();
        var files = new[] { Files.Table("core_0001_items.sql", "items") };
        await new SchemaMigrator(Dialect).RunAsync(connection, files, Cancellation);

        var report = await new SchemaMigrator(Dialect).RunAsync(connection, files, Cancellation);

        Assert.Empty(report.Applied);
        Assert.Equal(1, report.UpToDate);
    }

    [Fact]
    public async Task AnEditedFileIsRefused()
    {
        await using var connection = await OpenEmptyDatabaseAsync();
        await new SchemaMigrator(Dialect).RunAsync(connection, [Files.Table("core_0001_items.sql", "items")], Cancellation);

        var edited = Files.Table("core_0001_items.sql", "things");
        var error = await Assert.ThrowsAsync<MigrationException>(
            () => new SchemaMigrator(Dialect).RunAsync(connection, [edited], Cancellation));

        Assert.Contains("has changed", error.Message);
    }

    [Fact]
    public async Task AReusedNumberIsRefused()
    {
        await using var connection = await OpenEmptyDatabaseAsync();
        await new SchemaMigrator(Dialect).RunAsync(connection, [Files.Table("core_0001_items.sql", "items")], Cancellation);

        var reused = Files.Table("core_0001_other.sql", "items");
        var error = await Assert.ThrowsAsync<MigrationException>(
            () => new SchemaMigrator(Dialect).RunAsync(connection, [reused], Cancellation));

        Assert.Contains("reuses a number", error.Message);
    }

    [Fact]
    public async Task AFileNumberedBeforeAnAppliedOneIsRefused()
    {
        await using var connection = await OpenEmptyDatabaseAsync();
        await new SchemaMigrator(Dialect).RunAsync(connection, [Files.Table("core_0002_items.sql", "items")], Cancellation);

        var files = new[] { Files.Table("core_0001_early.sql", "early"), Files.Table("core_0002_items.sql", "items") };
        var error = await Assert.ThrowsAsync<MigrationException>(
            () => new SchemaMigrator(Dialect).RunAsync(connection, files, Cancellation));

        Assert.Contains("core_0001_early.sql is not applied", error.Message);
    }

    [Fact]
    public async Task AFailedFileLeavesNothingBehind()
    {
        await using var connection = await OpenEmptyDatabaseAsync();
        var files = new[]
        {
            Files.Table("core_0001_items.sql", "items"),
            Files.Create("core_0002_broken.sql", "CREATE TABLE half (id integer PRIMARY KEY); SELECT * FROM missing_table;"),
        };

        var error = await Assert.ThrowsAsync<MigrationException>(
            () => new SchemaMigrator(Dialect).RunAsync(connection, files, Cancellation));

        Assert.StartsWith("core_0002_broken.sql failed", error.Message);
        Assert.Equal(1, await connection.ExecuteScalarAsync<long>("SELECT COUNT(*) FROM schema_migrations"));
        await Assert.ThrowsAnyAsync<DbException>(() => connection.ExecuteScalarAsync<long>("SELECT COUNT(*) FROM half"));
    }

    [Fact]
    public async Task PrefixesRunInTheOrderTheyAreGiven()
    {
        await using var connection = await OpenEmptyDatabaseAsync();
        var files = new[]
        {
            Files.Table("core_0007_items.sql", "items"),
            Files.Create("sso_0001_rows.sql", "INSERT INTO items (id) VALUES (1);"),
        };

        var report = await new SchemaMigrator(Dialect).RunAsync(connection, files, Cancellation);

        Assert.Equal(["core_0007_items.sql", "sso_0001_rows.sql"], report.Applied.Select(f => f.FileName));
    }

    [Fact]
    public async Task FilesAppliedByANewerBuildAreIgnored()
    {
        await using var connection = await OpenEmptyDatabaseAsync();
        var newer = new[] { Files.Table("core_0001_items.sql", "items"), Files.Table("core_0002_more.sql", "more") };
        await new SchemaMigrator(Dialect).RunAsync(connection, newer, Cancellation);

        var report = await new SchemaMigrator(Dialect).RunAsync(connection, newer[..1], Cancellation);

        Assert.Empty(report.Applied);
    }

    [Fact]
    public async Task ConcurrentRunsApplyEachFileOnce()
    {
        await using var first = await OpenEmptyDatabaseAsync();
        var files = Enumerable.Range(1, 5)
            .Select(i => Files.Create($"core_{i:D4}_step.sql", $"CREATE TABLE step_{i} (id integer PRIMARY KEY);"))
            .ToArray();
        var connections = Enumerable.Range(0, 4).Select(_ => OpenSameDatabase(first)).ToList();

        var reports = await Task.WhenAll(connections.Select(c => new SchemaMigrator(Dialect).RunAsync(c, files, Cancellation)));

        Assert.Equal(files.Length, reports.Sum(r => r.Applied.Count));
        Assert.Equal(files.Length, await first.ExecuteScalarAsync<long>("SELECT COUNT(*) FROM schema_migrations"));
        foreach (var connection in connections)
        {
            await connection.DisposeAsync();
        }
    }
}
