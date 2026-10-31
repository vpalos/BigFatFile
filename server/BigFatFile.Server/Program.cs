using BigFatFile.Server.Auth;
using BigFatFile.Server.Health;
using BigFatFile.Server.Hosting;
using Microsoft.EntityFrameworkCore;

var command = args.FirstOrDefault();
if (command == "health")
{
    return await HealthCommand.RunAsync();
}

var app = command switch
{
    "migrate" => OriginHost.Build(args[1..]),
    "print-auth-schema" => OriginHost.Build(args[1..], logToStandardError: true),
    _ => OriginHost.Build(args),
};

switch (command)
{
    case "migrate":
        await StartupTasks.MigrateAsync(app, CancellationToken.None);
        return 0;

    case "print-auth-schema":
        await using (var scope = app.Services.CreateAsyncScope())
        {
            Console.WriteLine(scope.ServiceProvider.GetRequiredService<AuthDbContext>().Database.GenerateCreateScript());
        }

        return 0;
}

if (!OriginHost.IsGeneratingOpenApiDocument)
{
    await StartupTasks.PrepareAsync(app, app.Lifetime.ApplicationStopping);
}

await app.RunAsync();
return 0;

public partial class Program;
