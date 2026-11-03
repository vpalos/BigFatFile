using BigFatFile.Origin.Auth.Tokens;
using BigFatFile.Origin.Database;
using BigFatFile.Origin.Database.Seeds;

namespace BigFatFile.Origin.Server;

/// <summary>
/// What runs between building the app and serving the first request, in this order.
/// </summary>
internal static class StartupTasks
{
    public static async Task MigrateAsync(WebApplication app, CancellationToken cancellationToken)
    {
        app.Services.GetRequiredService<OriginSettings>().Validate();
        await app.Services.GetRequiredService<DatabaseMigrations>().RunAsync(cancellationToken);
        await app.Services.GetRequiredService<DatabaseRoles>().PrepareAsync(cancellationToken);
    }

    public static async Task PrepareAsync(WebApplication app, CancellationToken cancellationToken)
    {
        CongestionControlCheck.WarnIfNotBbr(app.Logger);
        await MigrateAsync(app, cancellationToken);
        await app.Services.GetRequiredService<ServerKeys>().LoadAsync(cancellationToken);

        if (app.Services.GetRequiredService<OriginSettings>().DevelopmentSeed)
        {
            await using var scope = app.Services.CreateAsyncScope();
            await scope.ServiceProvider.GetRequiredService<DevelopmentSeed>().RunAsync(cancellationToken);
        }
    }
}
