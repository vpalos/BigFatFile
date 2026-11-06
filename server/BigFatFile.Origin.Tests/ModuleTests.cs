using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using BigFatFile.Abstractions.Licensing;
using BigFatFile.Abstractions.Modules;
using BigFatFile.Contracts.Api;
using BigFatFile.Origin.Database;
using BigFatFile.Origin.Database.Seeds;
using BigFatFile.Origin.Licensing;
using BigFatFile.Origin.Models;
using BigFatFile.Origin.Modules;
using Dapper;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace BigFatFile.Origin.Tests;

public sealed class ModuleTests(OriginWithSampleModule origin) : IClassFixture<OriginWithSampleModule>
{
    [Fact]
    public async Task TheSampleModuleMapsItsEndpoints()
    {
        using var client = origin.CreateClient();

        var text = await client.GetStringAsync("/api/v1/sample/hello", TestContext.Current.CancellationToken);

        Assert.Equal("hello from the sample module", text);
    }

    [Fact]
    public async Task TheSpecListsTheModulesEndpoints()
    {
        using var client = origin.CreateClient();

        var spec = JsonNode.Parse(await client.GetStringAsync("/api/v1/openapi.json", TestContext.Current.CancellationToken))!;

        var tags = spec["paths"]?["/api/v1/sample/hello"]?["get"]?["tags"]?.AsArray().Select(tag => tag!.GetValue<string>());
        Assert.Equal(["sample"], tags);
    }

    [Fact]
    public async Task AModulesPlanReplacesTheCoresPlan()
    {
        using var browser = origin.CreateClient();
        await SignIn.SignInWithPasswordAsync(browser, DevelopmentSeed.UserEmail, DevelopmentSeed.UserPassword);
        var me = await browser.GetFromJsonAsync<Me>("/api/v1/account/me", TestContext.Current.CancellationToken);

        var tenant = await browser.GetFromJsonAsync<Tenant>($"/api/v1/tenants/{me!.TenantId}", TestContext.Current.CancellationToken);

        Assert.Equal("sample", tenant!.Plan);
    }

    [Fact]
    public void TwoModulesWithAPlanPolicyStopStartup()
    {
        LoadedModule[] modules = [Fake("first", withPlanPolicy: true), Fake("second", withPlanPolicy: false), Fake("third", withPlanPolicy: true)];

        var error = Assert.Throws<InvalidOperationException>(() => ModuleLoader.ConfigureServices(new ServiceCollection(), modules, Context()));

        Assert.Contains("first (/modules/first/first.dll), third (/modules/third/third.dll)", error.Message);
    }

    [Fact]
    public void OneModuleWithAPlanPolicyStartsUp()
    {
        var services = new ServiceCollection();

        ModuleLoader.ConfigureServices(services, [Fake("first", withPlanPolicy: true), Fake("second", withPlanPolicy: false)], Context());

        Assert.Single(services, service => service.ServiceType == typeof(IPlanPolicy));
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
    public void TheCoreFindsItsScriptsInItsMigrationsFolder() =>
        Assert.Equal("BigFatFile.Origin.Database.Migrations.", DatabaseMigrations.ScriptFilter(typeof(DatabaseMigrations)));

    [Fact]
    public void AScriptOutsideTheMigrationsFolderStopsStartup()
    {
        // The Models namespace has no Migrations folder, so every script of the assembly is outside it.
        var error = Assert.Throws<InvalidOperationException>(() => DatabaseMigrations.ScriptFilter(typeof(TenantModel)));

        Assert.Contains("BigFatFile.Origin.Database.Migrations.V0001__auth.sql", error.Message);
    }

    [Fact]
    public void ReservedKeysAreOffWithoutALicence()
    {
        var gate = new LicenceFeatureGate();

        Assert.All(FeatureKeys.All, key => Assert.False(gate.IsEnabled(key)));
        Assert.Throws<ArgumentException>(() => gate.IsEnabled("sso.typo"));
    }

    private static LoadedModule Fake(string name, bool withPlanPolicy) =>
        new(new FakeModule(name, withPlanPolicy), typeof(FakeModule).Assembly, $"/modules/{name}/{name}.dll");

    private static BffModuleContext Context() => new(new ConfigurationBuilder().Build(), new LicenceFeatureGate());

    private sealed class FakeModule(string name, bool withPlanPolicy) : IBffModule
    {
        public string Name => name;

        public Version RequiredCoreVersion => typeof(IBffModule).Assembly.GetName().Version!;

        public void ConfigureServices(IServiceCollection services, BffModuleContext context)
        {
            if (withPlanPolicy)
            {
                services.AddSingleton<IPlanPolicy, UnlimitedPlanPolicy>();
            }
        }

        public void MapEndpoints(IEndpointRouteBuilder endpoints)
        {
        }
    }
}
