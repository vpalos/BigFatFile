using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace BigFatFile.Abstractions.Modules;

/// <summary>
/// A module the Origin loads at startup from the "modules" folder.
/// It's mandatory that the class needs a public constructor without any parameters.
/// SQL migrations embedded in the module's assembly are run after the core's own
/// migrations, using the module's file prefix.
/// </summary>
public interface IBffModule
{
    /// <summary>The lowercase name, and also the module's prefix: <c>/api/&lt;name&gt;/</c>.</summary>
    string Name { get; }

    /// <summary>
    /// The core version the module was built against. The Origin loads the module only when
    /// its own major version is the same and its minor version is the same or higher.
    /// </summary>
    Version RequiredCoreVersion { get; }

    void ConfigureServices(IServiceCollection services, BffModuleContext context);

    /// <param name="endpoints">A route group already prefixed with <c>/api/&lt;name&gt;</c>.</param>
    void MapEndpoints(IEndpointRouteBuilder endpoints);
}
