using BigFatFile.Contracts.Account;
using BigFatFile.Server.Api;
using BigFatFile.Server.Auth;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Identity;

namespace BigFatFile.Server.Account;

internal static class SessionEndpoints
{
    public static void MapSessionEndpoints(this IEndpointRouteBuilder account)
    {
        account.MapGet("/options", GetOptions)
            .WithName("getAccountOptions")
            .WithSummary("Get account options")
            .WithDescription("""
                Returns what the sign-in and sign-up screens offer on this server.

                - `signUp` says whether anyone can create an account, or only people with an invitation.
                - `providers` lists the external accounts that people can sign in with, such as Google or Microsoft.
                """)
            .AllowAnonymous();

        account.MapGet("/csrf", GetCsrfToken)
            .WithName("getCsrfToken")
            .WithSummary("Get a CSRF token")
            .WithDescription("""
                Returns a token that protects against cross-site request forgery (CSRF). In that attack, another website makes the browser send a request, and the browser adds the session cookie to it.

                - Send the token in the `X-CSRF-Token` header of every POST, PUT, PATCH and DELETE request that uses the session cookie.
                - Get a new token after signing in or out, because each token belongs to one user.
                - Requests with a bearer token do not need it.
                """)
            .AllowAnonymous();

        account.MapPost("/sign-in", SignInAsync)
            .WithName("signIn")
            .WithSummary("Sign in with a password")
            .WithDescription("""
                Checks the email address and the password, and starts a session. The response sets the session cookie.

                The session works in one tenant. It is the tenant in `tenantId` when the request has one. Otherwise it is the tenant that the user signed in to last.
                """)
            .ProducesError(ApiErrors.WrongCredentials)
            .ProducesError(ApiErrors.EmailNotConfirmed)
            .ProducesError(ApiErrors.LockedOut)
            .ProducesError(ApiErrors.NotFound, "Here, the user is not a member of the tenant in `tenantId`.")
            .ProducesError(ApiErrors.TooManyRequests, $"Here, the limit is {SignInRateLimit.AttemptsPerMinute} sign-in attempts per minute from one IP address, for any accounts. All the addresses of one IPv6 /64 block count as one address.")
            .RequireRateLimiting(SignInRateLimit.Policy)
            .AllowAnonymous();

        account.MapPost("/sign-out", SignOutAsync)
            .WithName("signOut")
            .WithSummary("Sign out")
            .WithDescription("""
                Ends the session and removes the session cookie. The request must send the `X-CSRF-Token` header.
                """)
            .ProducesError(ApiErrors.CsrfTokenInvalid)
            .RequireAuthorization(AuthServices.SessionPolicy);

        account.MapPut("/tenant", SwitchTenantAsync)
            .WithName("switchTenant")
            .WithSummary("Switch tenant")
            .WithDescription("""
                Moves the session to another tenant that the user is a member of. The response sets a new session cookie, so every open tab of the web app switches too.
                """)
            .ProducesError(ApiErrors.NotFound, "Here, the user is not a member of that tenant.")
            .RequireAuthorization(AuthServices.SessionPolicy);
    }

    private static Ok<AccountOptions> GetOptions() =>
        throw new NotImplementedException();

    private static Ok<CsrfToken> GetCsrfToken(HttpContext context) =>
        throw new NotImplementedException();

    private static Task<Results<NoContent, ProblemHttpResult>> SignInAsync(SignInRequest request, HttpContext context) =>
        throw new NotImplementedException();

    private static async Task<Results<NoContent, ProblemHttpResult>> SignOutAsync(
        HttpContext context, IAntiforgery antiforgery, SignInManager<BffUser> signIn)
    {
        // The antiforgery middleware refuses only requests to endpoints that bind form fields.
        if (!await antiforgery.IsRequestValidAsync(context))
        {
            return Problems.Of(ApiErrors.CsrfTokenInvalid);
        }

        await signIn.SignOutAsync();
        return TypedResults.NoContent();
    }

    private static Task<Results<NoContent, ProblemHttpResult>> SwitchTenantAsync(SwitchTenantRequest request, HttpContext context) =>
        throw new NotImplementedException();
}
