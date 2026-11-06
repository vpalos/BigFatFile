using System.Net;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace BigFatFile.Origin.Tests;

/// <summary>An Origin on its own empty database, with the development seed.</summary>
public class Origin(Postgres postgres) : IAsyncLifetime
{
    /// <summary>The address of every request from <see cref="CreateClient"/>, because the test client has none.</summary>
    public static readonly IPAddress ClientAddress = IPAddress.Parse("192.0.2.10");

    private WebApplicationFactory<Program>? factory;

    /// <summary>The owner's connection string. Row-level security does not apply to it.</summary>
    public string ConnectionString { get; private set; } = "";

    /// <summary>The connection string that requests use. The server creates this user at startup.</summary>
    public string AppConnectionString => new NpgsqlConnectionStringBuilder(ConnectionString)
    {
        Username = "bigfatfile_app",
        Password = "bigfatfile-app-test",
    }.ConnectionString;

    protected virtual string ModulesPath => Path.Combine(AppContext.BaseDirectory, "no-modules");

    public WebApplicationFactory<Program> Factory => factory ?? throw new InvalidOperationException("Not started.");

    public HttpClient CreateClient() =>
        Factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = true });

    public async ValueTask InitializeAsync()
    {
        ConnectionString = await postgres.CreateDatabaseAsync();
        factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder => builder
            .UseEnvironment("Development")
            .UseSetting("BFF_DATABASE", AppConnectionString)
            .UseSetting("BFF_DATABASE_OWNER", ConnectionString)
            .UseSetting("BFF_DEVELOPMENT_SEED", "true")
            .UseSetting("BFF_MODULES_PATH", ModulesPath)
            .ConfigureServices(services => services.AddSingleton<IStartupFilter, ClientAddressFilter>()));
        factory.CreateClient().Dispose();
    }

    public async ValueTask DisposeAsync()
    {
        if (factory is not null)
        {
            await factory.DisposeAsync();
        }
    }

    private sealed class ClientAddressFilter : IStartupFilter
    {
        public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app =>
        {
            app.Use((context, nextMiddleware) =>
            {
                context.Connection.RemoteIpAddress ??= ClientAddress;
                return nextMiddleware(context);
            });
            next(app);
        };
    }
}

public sealed class OriginWithSampleModule(Postgres postgres) : Origin(postgres)
{
    protected override string ModulesPath => Path.Combine(AppContext.BaseDirectory, "modules");
}
