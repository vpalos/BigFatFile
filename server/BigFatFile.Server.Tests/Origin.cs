using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace BigFatFile.Server.Tests;

/// <summary>An Origin on its own empty database, with the development seed.</summary>
public class Origin(Postgres postgres) : IAsyncLifetime
{
    private WebApplicationFactory<Program>? factory;

    public string ConnectionString { get; private set; } = "";

    protected virtual string ModulesPath => Path.Combine(AppContext.BaseDirectory, "no-modules");

    public WebApplicationFactory<Program> Factory => factory ?? throw new InvalidOperationException("Not started.");

    public HttpClient CreateClient() =>
        Factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = true });

    public async ValueTask InitializeAsync()
    {
        ConnectionString = await postgres.CreateDatabaseAsync();
        factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder => builder
            .UseEnvironment("Development")
            .UseSetting("BFF_DATABASE", ConnectionString)
            .UseSetting("BFF_DEV_SEED", "true")
            .UseSetting("BFF_MODULES_PATH", ModulesPath));
        factory.CreateClient().Dispose();
    }

    public async ValueTask DisposeAsync()
    {
        if (factory is not null)
        {
            await factory.DisposeAsync();
        }
    }
}

public sealed class OriginWithSampleModule(Postgres postgres) : Origin(postgres)
{
    protected override string ModulesPath => Path.Combine(AppContext.BaseDirectory, "modules");
}
