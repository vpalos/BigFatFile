using BigFatFile.Server.Modules;
using EvolveDb;
using EvolveDb.Dialect;
using Npgsql;

namespace BigFatFile.Server.Database;

internal sealed class DatabaseMigrations(
    [FromKeyedServices(DatabaseServices.Owner)] NpgsqlDataSource dataSource, ModuleCatalog modules, ILogger<DatabaseMigrations> logger)
{
    public const string TablePrefix = "schema_migrations_";

    public async Task RunAsync(CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        Migrate(connection, "core", typeof(DatabaseMigrations));
        foreach (var loaded in modules.Modules)
        {
            Migrate(connection, loaded.Module.Name, loaded.Module.GetType());
        }
    }

    // The scripts are in the Migrations folder next to the type.
    private void Migrate(NpgsqlConnection connection, string prefix, Type type) =>
        new Evolve(connection, message => logger.LogInformation("{Prefix} migrations: {Message}", prefix, message), DBMS.PostgreSQL)
        {
            MetadataTableName = TablePrefix + prefix,
            EmbeddedResourceAssemblies = [type.Assembly],
            EmbeddedResourceFilters = [$"{type.Namespace}.Migrations."],
            EnableClusterMode = true,

            // A script may run longer than the 30-second default of Npgsql.
            CommandTimeout = 0,
        }.Migrate();
}
