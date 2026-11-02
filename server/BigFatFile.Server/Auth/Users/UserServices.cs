using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace BigFatFile.Server.Auth.Users;

internal static class UserServices
{
    /// <summary>This many wrong passwords in a row lock the account for <see cref="LockoutMinutes"/>.</summary>
    public const int LockoutAttempts = 10;
    public const int LockoutMinutes = 5;

    /// <param name="persistKeys">False only while the build writes the OpenAPI document, when there is no database.</param>
    public static IServiceCollection AddUsers(this IServiceCollection services, bool persistKeys)
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
                options.Lockout.MaxFailedAccessAttempts = LockoutAttempts;
                options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(LockoutMinutes);
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
            options.Events.OnRedirectToLogin = context => RedirectOnlyForConnect(context, StatusCodes.Status401Unauthorized);
            options.Events.OnRedirectToAccessDenied = context => RedirectOnlyForConnect(context, StatusCodes.Status403Forbidden);
        });

        // Every 30 minutes the security stamp check rebuilds the session from the user. The
        // tenant belongs to the session, not to the user, so it is copied over.
        services.Configure<SecurityStampValidatorOptions>(options => options.OnRefreshingPrincipal = context =>
        {
            var tenant = context.CurrentPrincipal?.FindFirst(TenantClaims.TenantId);
            if (tenant is not null && context.NewPrincipal?.Identity is ClaimsIdentity identity)
            {
                identity.AddClaim(new Claim(tenant.Type, tenant.Value));
            }

            return Task.CompletedTask;
        });

        return services;
    }

    // Only the authorization endpoint sends a browser to the sign-in page. The JSON endpoints,
    // all under /api, answer with the status instead.
    private static Task RedirectOnlyForConnect(RedirectContext<CookieAuthenticationOptions> context, int status)
    {
        if (context.Request.Path.StartsWithSegments("/connect"))
        {
            context.Response.Redirect(context.RedirectUri);
        }
        else
        {
            context.Response.StatusCode = status;
        }

        return Task.CompletedTask;
    }
}
