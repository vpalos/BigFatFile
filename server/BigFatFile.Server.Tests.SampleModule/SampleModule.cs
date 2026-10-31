using BigFatFile.Abstractions.Modules;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace BigFatFile.Server.Tests.SampleModule;

public sealed class SampleModule : IBffModule
{
    public string Name => "sample";

    public Version RequiredCoreVersion => typeof(IBffModule).Assembly.GetName().Version!;

    public void ConfigureServices(IServiceCollection services, BffModuleContext context) =>
        services.AddSingleton(new Greeting("hello from the sample module"));

    public void MapEndpoints(IEndpointRouteBuilder endpoints) =>
        endpoints.MapGet("/hello", (Greeting greeting) => Results.Text(greeting.Text)).AllowAnonymous();

    public sealed record Greeting(string Text);
}
