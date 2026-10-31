using BigFatFile.Migrator;

namespace BigFatFile.Migrator.Tests;

internal static class Files
{
    public static MigrationFile Create(string fileName, string sql) => MigrationFile.Parse(fileName, sql);

    public static MigrationFile Table(string fileName, string table) =>
        MigrationFile.Parse(fileName, $"CREATE TABLE {table} (id integer PRIMARY KEY);");
}
