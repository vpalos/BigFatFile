using System.Reflection;

namespace BigFatFile.Migrator.Tests;

public sealed class MigrationFileTests
{
    [Theory]
    [InlineData("core_0001_auth.sql", "core", 1, "auth")]
    [InlineData("sso_0012_tenant_providers.sql", "sso", 12, "tenant_providers")]
    [InlineData("sync_123456_big_number.sql", "sync", 123456, "big_number")]
    public void ParseReadsTheParts(string fileName, string prefix, int number, string name)
    {
        var file = MigrationFile.Parse(fileName, "SELECT 1;");

        Assert.Equal((prefix, number, name, fileName), (file.Prefix, file.Number, file.Name, file.FileName));
    }

    [Theory]
    [InlineData("core_1_auth.sql")]
    [InlineData("Core_0001_auth.sql")]
    [InlineData("core-0001-auth.sql")]
    [InlineData("core_0001_auth.SQL")]
    [InlineData("core_0001_.sql")]
    [InlineData("0001_auth.sql")]
    [InlineData("core_0001_auth.v2.sql")]
    public void ParseRejectsMalformedNames(string fileName)
    {
        Assert.Throws<MigrationException>(() => MigrationFile.Parse(fileName, "SELECT 1;"));
    }

    [Fact]
    public void LineEndingsDoNotChangeTheChecksum()
    {
        var lf = MigrationFile.Parse("core_0001_a.sql", "CREATE TABLE a (id int);\nCREATE TABLE b (id int);\n");
        var crlf = MigrationFile.Parse("core_0001_a.sql", "CREATE TABLE a (id int);\r\nCREATE TABLE b (id int);\r\n");
        var bom = MigrationFile.Parse("core_0001_a.sql", "﻿CREATE TABLE a (id int);\nCREATE TABLE b (id int);\n");

        Assert.Equal(lf.Checksum, crlf.Checksum);
        Assert.Equal(lf.Checksum, bom.Checksum);
    }

    [Fact]
    public void TheMarkerOnTheFirstLineTurnsOffTheTransaction()
    {
        Assert.False(MigrationFile.Parse("core_0001_a.sql", "-- no-transaction\nCREATE INDEX CONCURRENTLY a ON t (x);").RunsInTransaction);
        Assert.True(MigrationFile.Parse("core_0001_a.sql", "CREATE TABLE a (id int);\n-- no-transaction").RunsInTransaction);
    }

    [Fact]
    public void EmbeddedResourcesAreFound()
    {
        var files = EmbeddedMigrations.From(Assembly.GetExecutingAssembly());

        Assert.Equal(["test_0001_first.sql", "test_0002_second.sql"], files.Select(f => f.FileName).Order());
    }
}
