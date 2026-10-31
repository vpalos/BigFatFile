using System.Data.Common;

namespace BigFatFile.Migrator;

/// <summary>
/// How the migrator locks, opens transactions and creates its table on one database engine.
/// </summary>
public abstract class MigrationDialect
{
    public static MigrationDialect Postgres { get; } = new PostgresDialect();

    public static MigrationDialect Sqlite { get; } = new SqliteDialect();

    internal abstract string CreateTableSql { get; }

    internal string InsertSql =>
        "INSERT INTO schema_migrations (prefix, number, name, checksum) VALUES (@Prefix, @Number, @Name, @Checksum)";

    /// <summary>Holds a lock for the whole run, if the engine has one that outlives a transaction.</summary>
    internal abstract Task<IAsyncDisposable> LockRunAsync(DbConnection connection, Action onWait, CancellationToken cancellationToken);

    internal abstract Task<MigrationTransaction> BeginAsync(DbConnection connection, CancellationToken cancellationToken);
}
