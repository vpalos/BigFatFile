using System.Data.Common;
using Dapper;

namespace BigFatFile.Migrator;

internal sealed class SqliteDialect : MigrationDialect
{
    internal override string CreateTableSql => """
        CREATE TABLE IF NOT EXISTS schema_migrations (
            prefix     TEXT    NOT NULL,
            number     INTEGER NOT NULL,
            name       TEXT    NOT NULL,
            checksum   TEXT    NOT NULL,
            applied_at TEXT    NOT NULL DEFAULT (strftime('%Y-%m-%dT%H:%M:%fZ', 'now')),
            PRIMARY KEY (prefix, number)
        )
        """;

    // SQLite has no lock that outlives a transaction. Each migration runs in BEGIN IMMEDIATE,
    // which takes the write lock up front, and the migrator re-checks the table inside it.
    internal override Task<IAsyncDisposable> LockRunAsync(
        DbConnection connection, Action onWait, CancellationToken cancellationToken) =>
        Task.FromResult<IAsyncDisposable>(NoLock.Instance);

    internal override async Task<MigrationTransaction> BeginAsync(DbConnection connection, CancellationToken cancellationToken)
    {
        await connection.ExecuteAsync(new CommandDefinition("BEGIN IMMEDIATE", cancellationToken: cancellationToken));
        return new MigrationTransaction(connection, null);
    }

    private sealed class NoLock : IAsyncDisposable
    {
        public static readonly NoLock Instance = new();

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
