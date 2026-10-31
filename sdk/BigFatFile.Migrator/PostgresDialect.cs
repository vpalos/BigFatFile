using System.Data.Common;
using Dapper;

namespace BigFatFile.Migrator;

internal sealed class PostgresDialect : MigrationDialect
{
    // The ASCII bytes of "BFF_MIGR". Any constant works, as long as every node uses the same one.
    private const long LockKey = 0x4246465F4D494752;

    internal override string CreateTableSql => """
        CREATE TABLE IF NOT EXISTS schema_migrations (
            prefix     text        NOT NULL,
            number     integer     NOT NULL,
            name       text        NOT NULL,
            checksum   text        NOT NULL,
            applied_at timestamptz NOT NULL DEFAULT now(),
            PRIMARY KEY (prefix, number)
        )
        """;

    internal override async Task<IAsyncDisposable> LockRunAsync(
        DbConnection connection, Action onWait, CancellationToken cancellationToken)
    {
        var acquired = await connection.ExecuteScalarAsync<bool>(
            new CommandDefinition("SELECT pg_try_advisory_lock(@LockKey)", new { LockKey }, cancellationToken: cancellationToken));
        if (!acquired)
        {
            onWait();
            await connection.ExecuteAsync(
                new CommandDefinition("SELECT pg_advisory_lock(@LockKey)", new { LockKey }, commandTimeout: 0, cancellationToken: cancellationToken));
        }

        return new AdvisoryLock(connection);
    }

    internal override async Task<MigrationTransaction> BeginAsync(DbConnection connection, CancellationToken cancellationToken) =>
        new(connection, await connection.BeginTransactionAsync(cancellationToken));

    private sealed class AdvisoryLock(DbConnection connection) : IAsyncDisposable
    {
        public async ValueTask DisposeAsync() =>
            await connection.ExecuteAsync("SELECT pg_advisory_unlock(@LockKey)", new { LockKey });
    }
}
