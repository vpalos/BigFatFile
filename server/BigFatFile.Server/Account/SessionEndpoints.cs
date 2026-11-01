using BigFatFile.Contracts.Account;
using BigFatFile.Contracts.Api;
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
            .WithDescription($"""
                Checks the email address and the password, and starts a session. The response sets the session cookie.

                The session works in one tenant. It is the tenant in `tenantId` when the request has one. Otherwise it is the tenant that the user signed in to last.

                Errors:

                - 401 with the code `wrong_credentials`: the address or the password is wrong.
                - 403 with the code `email_not_confirmed`: the address is not confirmed yet.
                - 403 with the code `locked_out`: the account is locked for {AuthServices.LockoutMinutes} minutes, because of {AuthServices.LockoutAttempts} wrong passwords in a row. During the lock, even the right password gets this answer.
                - 404: the user is not a member of the tenant in `tenantId`.
                - 429 with the code `too_many_requests`: this IP address made more than {SignInRateLimit.AttemptsPerMinute} sign-in attempts in one minute, for any accounts. The `Retry-After` header says how many seconds to wait. All the addresses of one IPv6 /64 block count as one address.
                """)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status429TooManyRequests)
            .RequireRateLimiting(SignInRateLimit.Policy)
            .AllowAnonymous();

        account.MapPost("/sign-out", SignOutAsync)
            .WithName("signOut")
            .WithSummary("Sign out")
            .WithDescription("""
                Ends the session and removes the session cookie.

                The request must send the `X-CSRF-Token` header. Without it, the answer is 400 with the code `csrf_token_invalid`.
                """)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .RequireAuthorization(AuthServices.SessionPolicy);

        account.MapPut("/tenant", SwitchTenantAsync)
            .WithName("switchTenant")
            .WithSummary("Switch tenant")
            .WithDescription("""
                Moves the session to another tenant that the user is a member of. The response sets a new session cookie, so every open tab of the web app switches too.

                Answers 404 when the user is not a member of that tenant.
                """)
            .ProducesProblem(StatusCodes.Status404NotFound)
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
            return Problems.Of(StatusCodes.Status400BadRequest, ErrorCodes.CsrfTokenInvalid, $"The {AuthServices.CsrfHeader} header is missing or wrong.");
        }

        await signIn.SignOutAsync();
        return TypedResults.NoContent();
    }

    private static Task<Results<NoContent, ProblemHttpResult>> SwitchTenantAsync(SwitchTenantRequest request, HttpContext context) =>
        throw new NotImplementedException();
}
