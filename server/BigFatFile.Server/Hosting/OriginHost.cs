using System.Reflection;
using BigFatFile.Abstractions.Features;
using BigFatFile.Abstractions.Modules;
using BigFatFile.Server.Api;
using BigFatFile.Server.Auth;
using BigFatFile.Server.Configuration;
using BigFatFile.Server.Database;
using BigFatFile.Server.Development;
using BigFatFile.Server.Features;
using BigFatFile.Server.Health;
using BigFatFile.Server.Jobs;
using BigFatFile.Server.Modules;
using BigFatFile.Server.Observability;
using Microsoft.Extensions.Logging.Console;

namespace BigFatFile.Server.Hosting;

internal static class OriginHost
{
    public static string ServerVersion { get; } =
        typeof(OriginHost).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "unknown";

    /// <summary>
    /// True while the build runs the app with a mock server to write <c>api/openapi.json</c>.
    /// There is no database then, so nothing may connect to one.
    /// </summary>
    public static bool IsGeneratingOpenApiDocument =>
        Assembly.GetEntryAssembly()?.GetName().Name == "GetDocument.Insider";

    /// <param name="logToStandardError">For commands whose standard output is their result.</param>
    public static WebApplication Build(string[] args, bool logToStandardError = false)
    {
        var builder = WebApplication.CreateBuilder(args);
        if (logToStandardError)
        {
            builder.Services.Configure<ConsoleLoggerOptions>(options => options.LogToStandardErrorThreshold = LogLevel.Trace);
        }

        builder.Services.AddSingleton(provider =>
            OriginSettings.Read(provider.GetRequiredService<IConfiguration>(), provider.GetRequiredService<IHostEnvironment>()));

        builder.WebHost.ConfigureKestrel(kestrel =>
        {
            // By default, Kestrel's window sizes are designed for web page content.
            // But uploads need larger HTTP/2 windows (Architecture.FileTransfer.md, section 5).
            kestrel.Limits.Http2.InitialStreamWindowSize = 8 * 1024 * 1024;
            kestrel.Limits.Http2.InitialConnectionWindowSize = 32 * 1024 * 1024;
        });

        builder.AddTelemetry(ServerVersion);
        builder.Services
            .AddDatabase()
            .AddAuth(persistKeys: !IsGeneratingOpenApiDocument)
            .AddJobs(runServer: !IsGeneratingOpenApiDocument)
            .AddApi()
            .AddHealth()
            .AddSingleton<DatabaseMigrations>()
            .AddScoped<DevSeed>();

        var features = new LicenceFeatureGate();
        builder.Services.AddSingleton(features).AddSingleton<IFeatureGate>(features);

        // Modules register services, so they load before the host is built.
        var modulesPath = OriginSettings.ModulesPathFrom(builder.Configuration);
        var modules = ModuleLoader.Load(modulesPath);
        var moduleContext = new BffModuleContext(builder.Configuration, features);
        foreach (var loaded in modules)
        {
            loaded.Module.ConfigureServices(builder.Services, moduleContext);
        }

        builder.Services.AddSingleton(new ModuleCatalog(modules));

        var app = builder.Build();
        LogModules(app.Logger, modules, modulesPath);

        app.UseExceptionHandler(new ExceptionHandlerOptions
        {
            // In Development, minimal APIs throw on bad requests instead of answering 400.
            StatusCodeSelector = exception => exception is BadHttpRequestException bad ? bad.StatusCode : StatusCodes.Status500InternalServerError,
        });
        app.UseStatusCodePages();
        app.UseAuthentication();
        app.UseAuthorization();
        app.UseAntiforgery();

        app.MapHealth();
        app.MapMetrics();
        app.MapApiDocs();
        app.MapConnectEndpoints();
        app.MapAccountEndpoints();
        app.MapApiV1();
        app.MapJobDashboard();
        foreach (var loaded in modules)
        {
            loaded.Module.MapEndpoints(app.MapGroup("/api/" + loaded.Module.Name));
        }

        return app;
    }

    private static void LogModules(ILogger logger, IReadOnlyList<LoadedModule> modules, string modulesPath)
    {
        if (modules.Count == 0)
        {
            logger.LogInformation("No modules in {Path}", modulesPath);
        }

        foreach (var loaded in modules)
        {
            logger.LogInformation("Loaded module {Name} from {Path}", loaded.Module.Name, loaded.Path);
        }
    }
}
