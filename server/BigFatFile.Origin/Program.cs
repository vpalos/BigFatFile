using BigFatFile.Origin.Auth.Users;
using BigFatFile.Origin.Server;
using BigFatFile.Origin.Server.Health;
using Microsoft.EntityFrameworkCore;

var command = args.FirstOrDefault();

switch (command)
{
    case "health":
        return await HealthCommand.RunAsync();

    case "migrate":
        {
            var commandApp = OriginServices.Build(args[1..]);
            await OriginBootstrap.MigrateAsync(commandApp, CancellationToken.None);
            return 0;
        }

    case "print-auth-schema":
        {
            var commandApp = OriginServices.Build(args[1..], logToStandardError: true);
            await using var scope = commandApp.Services.CreateAsyncScope();
            Console.WriteLine(scope.ServiceProvider.GetRequiredService<AuthDbContext>().Database.GenerateCreateScript());
            return 0;
        }
}

var app = OriginServices.Build(args);

if (!OriginServices.IsGeneratingOpenApiDocument)
{
    await OriginBootstrap.PrepareAsync(app, app.Lifetime.ApplicationStopping);
}

await app.RunAsync();
return 0;
