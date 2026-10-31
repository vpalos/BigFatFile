using BigFatFile.Migrator;
using BigFatFile.Server.Modules;
using Npgsql;

namespace BigFatFile.Server.Database;

internal sealed class DatabaseMigrations(NpgsqlDataSource dataSource, ModuleCatalog modules, ILogger<DatabaseMigrations> logger)
{
    public IReadOnlyList<MigrationFile> Files =>
    [
        .. EmbeddedMigrations.From(typeof(DatabaseMigrations).Assembly),
        .. modules.Modules.SelectMany(module => EmbeddedMigrations.From(module.Assembly)),
    ];

    public async Task RunAsync(CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await new SchemaMigrator(MigrationDialect.Postgres, logger).RunAsync(connection, Files, cancellationToken);
    }
}
