using BigFatFile.Origin.Auth;
using BigFatFile.Origin.Auth.SignIn;
using BigFatFile.Origin.Auth.Tokens;
using BigFatFile.Origin.Auth.Users;
using BigFatFile.Origin.Models;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Npgsql;
using OpenIddict.Abstractions;
using OpenIddict.Server.AspNetCore;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace BigFatFile.Origin.Api.Endpoints;

/// <summary>
/// The parts of the OpenID Connect endpoints that need BigFatFile's own decisions. OpenIddict
/// validates each request first and handles discovery, JWKS, revocation and introspection alone.
/// </summary>
internal static class ConnectEndpoints
{
    public static void MapConnectEndpoints(this IEndpointRouteBuilder connect)
    {
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
            return Forbid(OpenIddictConstants.Errors.ConsentRequired, "This client needs a consent screen, which this server does not have yet.");
        }

        var principal = await TokenIdentity.ForUserAsync(
            users,
            user,
            session.Principal!.GetClaim(TenantClaims.TenantId),
            request.GetScopes(),
            await scopes.ListResourcesAsync(request.GetScopes()).ToListAsync());
        return Results.SignIn(principal, authenticationScheme: OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
    }

    private static async Task<IResult> TokenAsync(
        HttpContext context, UserManager<BffUser> users, SignInManager<BffUser> signIn, NpgsqlDataSource dataSource)
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
                return Forbid(OpenIddictConstants.Errors.InvalidGrant, "The user can no longer sign in.");
            }

            var principal = await TokenIdentity.ForUserAsync(
                users,
                user,
                granted.Principal!.GetClaim(TenantClaims.TenantId),
                granted.Principal!.GetScopes(),
                granted.Principal!.GetResources());

            // A refresh only renews what the App or the CLI already has, so it writes no row.
            if (request.IsAuthorizationCodeGrantType() && TenantClaims.TenantOf(granted.Principal) is { } tenantId)
            {
                await SignInAudit.WriteAsync(
                    dataSource,
                    SignInAudit.Entry(context, tenantId, AuditModel.AppAuthorized, user.Id, agentClientId: request.ClientId),
                    context.RequestAborted);
            }

            return Results.SignIn(principal, authenticationScheme: OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
        }

        return Forbid(OpenIddictConstants.Errors.UnsupportedGrantType, "This grant type is not supported.");
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
                    [OpenIddictServerAspNetCoreConstants.Properties.Error] = OpenIddictConstants.Errors.InvalidToken,
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

    private static IResult Forbid(string error, string description) =>
        Results.Forbid(
            new AuthenticationProperties(new Dictionary<string, string?>
            {
                [OpenIddictServerAspNetCoreConstants.Properties.Error] = error,
                [OpenIddictServerAspNetCoreConstants.Properties.ErrorDescription] = description,
            }),
            [OpenIddictServerAspNetCoreDefaults.AuthenticationScheme]);
}
