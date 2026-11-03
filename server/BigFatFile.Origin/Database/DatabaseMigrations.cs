using BigFatFile.Origin.Modules;
using EvolveDb;
using EvolveDb.Dialect;
using Npgsql;

namespace BigFatFile.Origin.Database;

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

    /// <summary>
    /// The resource prefix of the scripts in the Migrations folder next to the type. It throws when
    /// the assembly embeds an SQL script anywhere else, because Evolve would skip it without an error.
    /// </summary>
    internal static string ScriptFilter(Type type)
    {
        var filter = $"{type.Namespace}.Migrations.";
        var outside = type.Assembly.GetManifestResourceNames()
            .Where(name => name.EndsWith(".sql", StringComparison.OrdinalIgnoreCase) && !name.StartsWith(filter, StringComparison.Ordinal))
            .Order(StringComparer.Ordinal)
            .ToList();
        if (outside.Count > 0)
        {
            throw new InvalidOperationException(
                $"{type.Assembly.GetName().Name} embeds SQL scripts that would never run: {string.Join(", ", outside)}. " +
                $"Migration scripts go in the Migrations folder next to {type.Name}, so that their resource names start with {filter}");
        }

        return filter;
    }

    private void Migrate(NpgsqlConnection connection, string prefix, Type type) =>
        new Evolve(connection, message => logger.LogInformation("{Prefix} migrations: {Message}", prefix, message), DBMS.PostgreSQL)
        {
            MetadataTableName = TablePrefix + prefix,
            EmbeddedResourceAssemblies = [type.Assembly],
            EmbeddedResourceFilters = [ScriptFilter(type)],
            EnableClusterMode = true,

            // A script may run longer than the 30-second default of Npgsql.
            CommandTimeout = 0,
        }.Migrate();
}
