using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace BigFatFile.Abstractions.Modules;

/// <summary>
/// A module the Origin loads at startup from the "modules" folder.
/// It's mandatory that the class needs a public constructor without any parameters.
/// The SQL scripts embedded from the Migrations folder next to the class run after the
/// core's own scripts, with <see cref="Name"/> as their prefix.
/// Startup fails when the module embeds an SQL script anywhere else.
/// </summary>
public interface IBffModule
{
    /// <summary>
    /// The lowercase name. It is also the module's prefix, <c>/api/v1/&lt;name&gt;/</c>, and the tag
    /// of its endpoints. It cannot be the name of a core group, such as <c>account</c>.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// The core version the module was built against. The Origin loads the module only when
    /// the core's major version is the same and the core's minor version is the same or higher.
    /// </summary>
    Version RequiredCoreVersion { get; }

    void ConfigureServices(IServiceCollection services, BffModuleContext context);

    /// <param name="endpoints">A route group already prefixed with <c>/api/v1/&lt;name&gt;</c>.</param>
    void MapEndpoints(IEndpointRouteBuilder endpoints);
}
