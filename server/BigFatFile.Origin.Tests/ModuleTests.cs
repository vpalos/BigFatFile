using System.Net;
using BigFatFile.Abstractions.Features;
using BigFatFile.Origin.Modules;
using Dapper;
using Npgsql;

namespace BigFatFile.Origin.Tests;

public sealed class ModuleTests(OriginWithSampleModule origin) : IClassFixture<OriginWithSampleModule>
{
    [Fact]
    public async Task TheSampleModuleMapsItsEndpoints()
    {
        using var client = origin.CreateClient();

        var text = await client.GetStringAsync("/api/sample/hello", TestContext.Current.CancellationToken);

        Assert.Equal("hello from the sample module", text);
    }

    [Fact]
    public async Task TheSampleModuleMigrationsRunAfterTheCore()
    {
        await using var connection = new NpgsqlConnection(origin.ConnectionString);

        var coreFinished = await connection.ExecuteScalarAsync<DateTime>(
            "SELECT max(installed_on) FROM schema_migrations_core WHERE type = 0");
        var sampleStarted = await connection.ExecuteScalarAsync<DateTime>(
            "SELECT min(installed_on) FROM schema_migrations_sample WHERE type = 0");

        Assert.True(coreFinished <= sampleStarted);
        Assert.Equal(0, await connection.ExecuteScalarAsync<long>("SELECT COUNT(*) FROM sample_notes"));
    }

    [Fact]
    public void ReservedKeysAreOffWithoutALicence()
    {
        var gate = new LicenceFeatureGate();

        Assert.All(FeatureKeys.All, key => Assert.False(gate.IsEnabled(key)));
        Assert.Throws<ArgumentException>(() => gate.IsEnabled("sso.typo"));
    }
}
