using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using OpenIddict.Server;
using OpenIddict.Server.AspNetCore;
using OpenIddict.Validation.AspNetCore;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace BigFatFile.Server.Auth;

internal static class AuthServices
{
    /// <summary>The default scheme: bearer tokens when the request has one, the session cookie otherwise.</summary>
    public const string Scheme = "bff";

    /// <param name="persistKeys">False only while the build writes the OpenAPI document, when there is no database.</param>
    public static IServiceCollection AddAuth(this IServiceCollection services, bool persistKeys)
    {
        services.AddDbContext<AuthDbContext>((provider, options) => options
            .UseNpgsql(provider.GetRequiredService<NpgsqlDataSource>())
            .UseSnakeCaseNamingConvention());

        var dataProtection = services.AddDataProtection().SetApplicationName("BigFatFile");
        if (persistKeys)
        {
            dataProtection.PersistKeysToDbContext<AuthDbContext>();
        }
        else
        {
            dataProtection.UseEphemeralDataProtectionProvider();
        }

        services.AddIdentity<BffUser, IdentityRole<Guid>>(options =>
            {
                options.Stores.SchemaVersion = IdentitySchemaVersions.Version3;
                options.User.RequireUniqueEmail = true;
                options.Password.RequiredLength = 12;
                options.Password.RequireDigit = false;
                options.Password.RequireLowercase = false;
                options.Password.RequireUppercase = false;
                options.Password.RequireNonAlphanumeric = false;
                options.ClaimsIdentity.UserIdClaimType = Claims.Subject;
                options.ClaimsIdentity.UserNameClaimType = Claims.Name;
                options.ClaimsIdentity.RoleClaimType = Claims.Role;
                options.ClaimsIdentity.EmailClaimType = Claims.Email;
            })
            .AddEntityFrameworkStores<AuthDbContext>()
            .AddDefaultTokenProviders();

        services.ConfigureApplicationCookie(options =>
        {
            options.Cookie.Name = "bff_session";
            options.LoginPath = "/account/sign-in";
            options.Events.OnRedirectToLogin = context => RejectApiRedirect(context, StatusCodes.Status401Unauthorized);
            options.Events.OnRedirectToAccessDenied = context => RejectApiRedirect(context, StatusCodes.Status403Forbidden);
        });

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

        services.AddAntiforgery();
        services.AddAuthorizationBuilder().AddScopePolicies();
        services.AddSingleton<IAuthorizationHandler, ScopeRequirementHandler>();

        // Plain HTTP is accepted only in Development, where the server runs on localhost.
        services.AddOptions<OpenIddictServerAspNetCoreOptions>().Configure<IHostEnvironment>((options, environment) =>
            options.DisableTransportSecurityRequirement = environment.IsDevelopment());

        services.AddSingleton<ServerKeys>();
        services.AddOptions<OpenIddictServerOptions>().Configure<ServerKeys>((options, keys) =>
        {
            options.SigningCredentials.Add(keys.Signing);
            options.EncryptionCredentials.Add(keys.Encryption);
        });

        services.AddOpenIddict()
            .AddCore(options => options
                .UseEntityFrameworkCore()
                .UseDbContext<AuthDbContext>()
                .ReplaceDefaultEntities<Guid>())
            .AddServer(options =>
            {
                options.SetAuthorizationEndpointUris("connect/authorize")
                    .SetTokenEndpointUris("connect/token")
                    .SetRevocationEndpointUris("connect/revoke")
                    .SetIntrospectionEndpointUris("connect/introspect")
                    .SetUserInfoEndpointUris("connect/userinfo");

                options.AllowAuthorizationCodeFlow()
                    .RequireProofKeyForCodeExchange()
                    .AllowRefreshTokenFlow()
                    .AllowClientCredentialsFlow();

                options.RegisterScopes([.. BffScopes.All]);

                // Access tokens stay plain signed JWTs, so a Replica can check them offline
                // with the public key from the JWKS endpoint.
                options.DisableAccessTokenEncryption();

                options.UseAspNetCore()
                    .EnableAuthorizationEndpointPassthrough()
                    .EnableTokenEndpointPassthrough()
                    .EnableUserInfoEndpointPassthrough()
                    .EnableStatusCodePagesIntegration();
            })
            .AddValidation(options =>
            {
                options.UseLocalServer();
                options.UseAspNetCore();
            });

        return services;
    }

    private static Task RejectApiRedirect(Microsoft.AspNetCore.Authentication.RedirectContext<Microsoft.AspNetCore.Authentication.Cookies.CookieAuthenticationOptions> context, int status)
    {
        if (context.Request.Path.StartsWithSegments("/api"))
        {
            context.Response.StatusCode = status;
        }
        else
        {
            context.Response.Redirect(context.RedirectUri);
        }

        return Task.CompletedTask;
    }
}
