using System.Net;
using BigFatFile.Abstractions.Features;
using BigFatFile.Server.Features;
using Dapper;
using Npgsql;

namespace BigFatFile.Server.Tests;

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

        var prefixes = await connection.QueryAsync<string>("SELECT prefix FROM schema_migrations ORDER BY applied_at, number");

        Assert.Equal(["core", "core", "core", "sample"], prefixes);
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
