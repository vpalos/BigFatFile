using BigFatFile.Origin.Auth.Tokens;
using BigFatFile.Origin.Auth.Users;
using Microsoft.AspNetCore.Identity;
using OpenIddict.Validation.AspNetCore;

namespace BigFatFile.Origin.Auth;

internal static class AuthServices
{
    /// <summary>The default scheme: bearer tokens when the request has one, the session cookie otherwise.</summary>
    public const string Scheme = "bff";

    /// <summary>For the account endpoints that act on the browser's session, which a bearer token cannot start or end.</summary>
    public const string SessionPolicy = "session";

    public const string CsrfHeader = "X-CSRF-Token";

    /// <param name="persistKeys">False only while the build writes the OpenAPI document, when there is no database.</param>
    public static IServiceCollection AddAuth(this IServiceCollection services, bool persistKeys)
    {
        services.AddUsers(persistKeys);
        services.AddTokens();

        services.AddAuthentication(options =>
            {
                options.DefaultScheme = Scheme;
                options.DefaultAuthenticateScheme = Scheme;
                options.DefaultChallengeScheme = Scheme;
                options.DefaultForbidScheme = Scheme;
            })
            .AddPolicyScheme(Scheme, "Bearer token or session cookie", options =>
                options.ForwardDefaultSelector = context =>
                    context.Request.Headers.Authorization.ToString().StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)
                        ? OpenIddictValidationAspNetCoreDefaults.AuthenticationScheme
                        : IdentityConstants.ApplicationScheme);

        services.AddAntiforgery(options => options.HeaderName = CsrfHeader);
        services.AddAuthorizationBuilder()
            .AddScopePolicies()
            .AddPolicy(SessionPolicy, policy => policy
                .AddAuthenticationSchemes(IdentityConstants.ApplicationScheme)
                .RequireAuthenticatedUser());

        return services;
    }
}
