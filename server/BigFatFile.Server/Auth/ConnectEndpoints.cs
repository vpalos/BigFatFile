using System.Security.Claims;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.IdentityModel.Tokens;
using OpenIddict.Abstractions;
using OpenIddict.Server.AspNetCore;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace BigFatFile.Server.Auth;

/// <summary>
/// The parts of the OpenID Connect endpoints that need BigFatFile's own decisions. OpenIddict
/// validates each request first and handles discovery, JWKS, revocation and introspection alone.
/// </summary>
internal static class ConnectEndpoints
{
    public static void MapConnectEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var connect = endpoints.MapGroup("/connect").ExcludeFromDescription();
        connect.MapMethods("/authorize", [HttpMethods.Get, HttpMethods.Post], AuthorizeAsync);
        connect.MapPost("/token", TokenAsync).DisableAntiforgery();
        connect.MapMethods("/userinfo", [HttpMethods.Get, HttpMethods.Post], UserInfoAsync).DisableAntiforgery();
    }

    private static async Task<IResult> AuthorizeAsync(
        HttpContext context,
        UserManager<BffUser> users,
        IOpenIddictApplicationManager applications,
        IOpenIddictScopeManager scopes)
    {
        var request = context.GetOpenIddictServerRequest()
            ?? throw new InvalidOperationException("OpenIddict did not attach the authorization request.");

        var session = await context.AuthenticateAsync(IdentityConstants.ApplicationScheme);
        var user = session.Succeeded ? await users.GetUserAsync(session.Principal) : null;
        if (user is null)
        {
            var query = context.Request.HasFormContentType ? QueryString.Create(context.Request.Form) : context.Request.QueryString;
            return Results.Challenge(
                new AuthenticationProperties { RedirectUri = context.Request.PathBase + context.Request.Path + query },
                [IdentityConstants.ApplicationScheme]);
        }

        var application = await applications.FindByClientIdAsync(request.ClientId!)
            ?? throw new InvalidOperationException("OpenIddict accepted a request from an unknown client.");

        // First-party clients are registered with implicit consent. This server has no
        // consent screen, so a client that needs one is refused.
        if (await applications.GetConsentTypeAsync(application) != ConsentTypes.Implicit)
        {
            return Forbid(Errors.ConsentRequired, "This client needs a consent screen, which this server does not have yet.");
        }

        var identity = await CreateUserIdentityAsync(users, user);
        identity.SetScopes(request.GetScopes());
        identity.SetResources(await scopes.ListResourcesAsync(identity.GetScopes()).ToListAsync());
        identity.SetDestinations(DestinationsOf);
        return Results.SignIn(new ClaimsPrincipal(identity), authenticationScheme: OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
    }

    private static async Task<IResult> TokenAsync(
        HttpContext context,
        UserManager<BffUser> users,
        SignInManager<BffUser> signIn,
        IOpenIddictApplicationManager applications,
        IOpenIddictScopeManager scopes)
    {
        var request = context.GetOpenIddictServerRequest()
            ?? throw new InvalidOperationException("OpenIddict did not attach the token request.");

        if (request.IsAuthorizationCodeGrantType() || request.IsRefreshTokenGrantType())
        {
            var granted = await context.AuthenticateAsync(OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
            var user = await users.FindByIdAsync(granted.Principal?.GetClaim(Claims.Subject) ?? "");

            // The user may have been locked or deleted since the code or refresh token was issued.
            if (user is null || !await signIn.CanSignInAsync(user))
            {
                return Forbid(Errors.InvalidGrant, "The user can no longer sign in.");
            }

            var identity = await CreateUserIdentityAsync(users, user);
            identity.SetScopes(granted.Principal!.GetScopes());
            identity.SetResources(granted.Principal!.GetResources());
            identity.SetDestinations(DestinationsOf);
            return Results.SignIn(new ClaimsPrincipal(identity), authenticationScheme: OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
        }

        if (request.IsClientCredentialsGrantType())
        {
            var application = await applications.FindByClientIdAsync(request.ClientId!)
                ?? throw new InvalidOperationException("OpenIddict accepted a request from an unknown client.");

            var identity = new ClaimsIdentity(TokenValidationParameters.DefaultAuthenticationType, Claims.Name, Claims.Role);
            identity.SetClaim(Claims.Subject, request.ClientId);
            identity.SetClaim(Claims.Name, await applications.GetDisplayNameAsync(application));
            identity.SetScopes(request.GetScopes());
            identity.SetResources(await scopes.ListResourcesAsync(identity.GetScopes()).ToListAsync());
            identity.SetDestinations(DestinationsOf);
            return Results.SignIn(new ClaimsPrincipal(identity), authenticationScheme: OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
        }

        return Forbid(Errors.UnsupportedGrantType, "This grant type is not supported.");
    }

    private static async Task<IResult> UserInfoAsync(HttpContext context, UserManager<BffUser> users)
    {
        var token = await context.AuthenticateAsync(OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
        var user = await users.FindByIdAsync(token.Principal?.GetClaim(Claims.Subject) ?? "");
        if (user is null)
        {
            return Results.Challenge(
                new AuthenticationProperties(new Dictionary<string, string?>
                {
                    [OpenIddictServerAspNetCoreConstants.Properties.Error] = Errors.InvalidToken,
                    [OpenIddictServerAspNetCoreConstants.Properties.ErrorDescription] = "The token's user no longer exists.",
                }),
                [OpenIddictServerAspNetCoreDefaults.AuthenticationScheme]);
        }

        var claims = new Dictionary<string, object> { [Claims.Subject] = user.Id.ToString() };
        if (token.Principal!.HasScope(BffScopes.Email))
        {
            claims[Claims.Email] = user.Email!;
            claims[Claims.EmailVerified] = user.EmailConfirmed;
        }

        if (token.Principal!.HasScope(BffScopes.Profile))
        {
            claims[Claims.Name] = user.UserName!;
        }

        return Results.Ok(claims);
    }

    private static async Task<ClaimsIdentity> CreateUserIdentityAsync(UserManager<BffUser> users, BffUser user)
    {
        var identity = new ClaimsIdentity(TokenValidationParameters.DefaultAuthenticationType, Claims.Name, Claims.Role);
        identity.SetClaim(Claims.Subject, user.Id.ToString())
            .SetClaim(Claims.Email, user.Email)
            .SetClaim(Claims.Name, user.UserName)
            .SetClaims(Claims.Role, [.. await users.GetRolesAsync(user)]);
        return identity;
    }

    private static IEnumerable<string> DestinationsOf(Claim claim) => claim.Type switch
    {
        Claims.Subject => [Destinations.AccessToken, Destinations.IdentityToken],
        Claims.Name when claim.Subject!.HasScope(BffScopes.Profile) => [Destinations.AccessToken, Destinations.IdentityToken],
        Claims.Email when claim.Subject!.HasScope(BffScopes.Email) => [Destinations.AccessToken, Destinations.IdentityToken],
        _ => [Destinations.AccessToken],
    };

    private static IResult Forbid(string error, string description) =>
        Results.Forbid(
            new AuthenticationProperties(new Dictionary<string, string?>
            {
                [OpenIddictServerAspNetCoreConstants.Properties.Error] = error,
                [OpenIddictServerAspNetCoreConstants.Properties.ErrorDescription] = description,
            }),
            [OpenIddictServerAspNetCoreDefaults.AuthenticationScheme]);
}
