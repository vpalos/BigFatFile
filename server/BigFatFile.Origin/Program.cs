using BigFatFile.Origin.Auth.Users;
using BigFatFile.Origin.Health;
using BigFatFile.Origin.Hosting;
using Microsoft.EntityFrameworkCore;

var command = args.FirstOrDefault();

switch (command)
{
    case "health":
        return await HealthCommand.RunAsync();

    case "migrate":
        {
            var commandApp = OriginHost.Build(args[1..]);
            await StartupTasks.MigrateAsync(commandApp, CancellationToken.None);
            return 0;
        }

    case "print-auth-schema":
        {
            var commandApp = OriginHost.Build(args[1..], logToStandardError: true);
            await using var scope = commandApp.Services.CreateAsyncScope();
            Console.WriteLine(scope.ServiceProvider.GetRequiredService<AuthDbContext>().Database.GenerateCreateScript());
            return 0;
        }
}

var app = OriginHost.Build(args);

if (!OriginHost.IsGeneratingOpenApiDocument)
{
    await StartupTasks.PrepareAsync(app, app.Lifetime.ApplicationStopping);
}

await app.RunAsync();
return 0;
