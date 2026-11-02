using System.Security.Claims;
using BigFatFile.Contracts.Account;
using BigFatFile.Contracts.Api;
using BigFatFile.Server.Api.Errors;
using BigFatFile.Server.Api.Parameters;
using BigFatFile.Server.Auth;
using BigFatFile.Server.Database;
using BigFatFile.Server.Models;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Identity;

namespace BigFatFile.Server.Api.Endpoints;

/// <summary>
/// The signed-in user, and the JSON endpoints behind the web app's account screens. The
/// screens' endpoints work only on the same origin, with the session cookie; Apps sign users
/// in through <c>/connect/authorize</c>.
/// </summary>
internal static class AccountEndpoints
{
    public static void MapAccountEndpoints(this IEndpointRouteBuilder account)
    {
        MapSession(account);
        MapSignIn(account);
        MapSignUp(account);
        MapPassword(account);
        MapInvitations(account.MapGroup("/invitations"));
        MapSignOut(account);
    }

    private static void MapSession(IEndpointRouteBuilder account)
    {
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

        account.MapGet("/me", GetMeAsync)
            .WithName("getMe")
            .WithSummary("Get the signed-in user")
            .WithDescription("""
                Returns the user that the token or session belongs to, and:

                - `tenantId`: the tenant that the token or session works in;
                - `tenants`: every tenant the user is a member of, with the user's role in each.
                """)
            .ProducesError(ApiErrors.AuthenticationRequired)
            .ProducesError(ApiErrors.Forbidden)
            .ProducesError(ApiErrors.UserRequired)
            .RequireAuthorization(ScopeAuthorization.PolicyFor(BffScopes.OpenId));

        account.MapPut("/tenant", SwitchTenantAsync)
            .WithName("switchTenant")
            .WithSummary("Switch tenant")
            .WithDescription("""
                Moves the session to another tenant that the user is a member of. The response sets a new session cookie, so every open tab of the web app switches too.
                """)
            .ProducesError(ApiErrors.NotFound, "Here, the user is not a member of that tenant.")
            .RequireAuthorization(AuthServices.SessionPolicy);
    }

    private static Ok<CsrfToken> GetCsrfToken(HttpContext context) =>
        throw new NotImplementedException();

    private static async Task<Results<Ok<Me>, ProblemHttpResult>> GetMeAsync(
        ClaimsPrincipal principal, UserManager<BffUser> users, RequestDatabase database, CancellationToken cancellationToken)
    {
        var user = TenantClaims.UserOf(principal) is { } userId ? await users.FindByIdAsync(userId.ToString()) : null;
        if (user is null)
        {
            return Problems.Of(ApiErrors.UserRequired);
        }

        var connection = await database.OpenAsync(cancellationToken);
        var tenants = await MemberModel.ListTenantsOfUserAsync(connection, user.Id, cancellationToken);

        return TypedResults.Ok(new Me(
            user.Id,
            user.Email!,
            user.UserName!,
            TenantClaims.TenantOf(principal),
            [.. tenants.Select(tenant => new MeWithinTenant(tenant.TenantId, tenant.TenantName, tenant.Role))]));
    }

    private static Task<Results<NoContent, ProblemHttpResult>> SwitchTenantAsync(SwitchTenantRequest request, HttpContext context) =>
        throw new NotImplementedException();

    private static void MapSignIn(IEndpointRouteBuilder account)
    {
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

        account.MapGet("/sign-in/{provider}", StartExternalSignInAsync)
            .WithName("signInExternally")
            .WithSummary("Sign in with an external account (OAuth)")
            .WithDescription("""
                Sends the browser to the sign-in page of another service, such as Google or Microsoft. When the person has signed in there, the browser comes back to `returnUrl`, which must be a path on this server.

                - `provider` is a `key` from `account.providers` in `GET /api/v1/meta`.
                - The web app opens this address as a page in the browser, not with a fetch call, because the browser must follow the redirects.
                """)
            .Produces(StatusCodes.Status302Found)
            .ProducesError(ApiErrors.NotFound, "Here, this server has not set up that provider.")
            .AllowAnonymous();
    }

    private static Task<Results<NoContent, ProblemHttpResult>> SignInAsync(SignInRequest request, HttpContext context) =>
        throw new NotImplementedException();

    private static Task<Results<ChallengeHttpResult, ProblemHttpResult>> StartExternalSignInAsync(EnumParameter<ExternalProvider> provider, string? returnUrl, HttpContext context) =>
        throw new NotImplementedException();

    private static void MapSignUp(IEndpointRouteBuilder account)
    {
        account.MapPost("/sign-up", SignUpAsync)
            .WithName("signUp")
            .WithSummary("Sign up")
            .WithDescription("""
                Creates an account with a name, an email address and a password.

                - With `invitationToken`, the invitation email proves that the person receives mail at the address. The account is created at once, joins the invited tenant, and its session starts. The outcome is `signed_in`.
                - Without it, a confirmation email is sent. The account can sign in once the address is confirmed. The outcome is `confirmation_sent`.
                - When the address already has an account, the outcome is also `confirmation_sent`, and that account gets an email saying so. This way the response does not reveal which addresses have accounts.
                """)
            .ProducesError(ApiErrors.InvalidFields, "For example, a password shorter than 12 characters.")
            .ProducesError(ApiErrors.SignUpClosed)
            .AllowAnonymous();

        account.MapPost("/confirm-email", ConfirmEmailAsync)
            .WithName("confirmEmail")
            .WithSummary("Confirm an email address")
            .WithDescription("""
                Confirms the address with the user ID and the code from the confirmation email, and starts a session. The link in the email opens the web app, which reads both values from it.

                On a server with open sign-up, this is also when the person's new tenant is created.

                Only the first confirmation of an address starts a session. So nobody can sign in later with a link that was forwarded or saved.
                """)
            .ProducesError(ApiErrors.InvalidCode, "It is also the answer when the user ID is unknown.")
            .ProducesError(ApiErrors.AlreadyConfirmed)
            .AllowAnonymous();

        account.MapPost("/resend-confirmation", ResendConfirmationAsync)
            .WithName("resendConfirmation")
            .WithSummary("Resend the confirmation email")
            .WithDescription("""
                Sends the confirmation email again, to an address that is not confirmed yet.

                The answer is always 202, whether or not the address has an account. This way the response does not reveal which addresses have accounts.
                """)
            .AllowAnonymous();
    }

    private static Task<Results<Ok<SignUpResult>, ProblemHttpResult>> SignUpAsync(SignUpRequest request, HttpContext context) =>
        throw new NotImplementedException();

    private static Task<Results<NoContent, ProblemHttpResult>> ConfirmEmailAsync(ConfirmEmailRequest request, HttpContext context) =>
        throw new NotImplementedException();

    private static Task<Accepted> ResendConfirmationAsync(EmailRequest request, CancellationToken cancellationToken) =>
        throw new NotImplementedException();

    private static void MapPassword(IEndpointRouteBuilder account)
    {
        account.MapPost("/forgot-password", ForgotPasswordAsync)
            .WithName("forgotPassword")
            .WithSummary("Request a password reset")
            .WithDescription("""
                Sends an email with a link to reset the password.

                The answer is always 202, whether or not the address has an account. This way the response does not reveal which addresses have accounts.
                """)
            .AllowAnonymous();

        account.MapPost("/reset-password", ResetPasswordAsync)
            .WithName("resetPassword")
            .WithSummary("Reset the password")
            .WithDescription("""
                Sets a new password, with the user ID and the code from the password reset email. The link in the email opens the web app, which reads both values from it.
                """)
            .ProducesError(ApiErrors.InvalidCode)
            .ProducesError(ApiErrors.InvalidFields, "For example, a new password shorter than 12 characters.")
            .AllowAnonymous();
    }

    private static Task<Accepted> ForgotPasswordAsync(EmailRequest request, CancellationToken cancellationToken) =>
        throw new NotImplementedException();

    private static Task<Results<NoContent, ProblemHttpResult>> ResetPasswordAsync(ResetPasswordRequest request, CancellationToken cancellationToken) =>
        throw new NotImplementedException();

    private static void MapInvitations(IEndpointRouteBuilder invitations)
    {
        invitations.MapPost("/lookup", LookUpInvitationAsync)
            .WithName("lookUpInvitation")
            .WithSummary("Look up an invitation")
            .WithDescription("""
                Returns what an invitation is for, so that the invitation screen can show it before the person accepts: the tenant, who sent it, the invited address and the role.

                - `accountExists` tells the screen whether to ask the person to sign in or to sign up.
                - The token is sent in the request body, so it stays out of request logs.
                """)
            .ProducesError(ApiErrors.NotFound, "Here, the token is unknown, has expired, was revoked or was already accepted.")
            .AllowAnonymous();

        invitations.MapPost("/accept", AcceptInvitationAsync)
            .WithName("acceptInvitation")
            .WithSummary("Accept an invitation")
            .WithDescription("""
                Adds the signed-in user to the invited tenant, with the invited role. The session then works in that tenant.
                """)
            .ProducesError(ApiErrors.NotFound, "Here, the token is unknown, has expired, was revoked or was already accepted.")
            .ProducesError(ApiErrors.WrongAccount)
            .RequireAuthorization(AuthServices.SessionPolicy);
    }

    private static Task<Results<Ok<InvitationPreview>, ProblemHttpResult>> LookUpInvitationAsync(InvitationTokenRequest request, CancellationToken cancellationToken) =>
        throw new NotImplementedException();

    private static Task<Results<NoContent, ProblemHttpResult>> AcceptInvitationAsync(InvitationTokenRequest request, HttpContext context) =>
        throw new NotImplementedException();

    private static void MapSignOut(IEndpointRouteBuilder account)
    {
        account.MapPost("/sign-out", SignOutAsync)
            .WithName("signOut")
            .WithSummary("Sign out")
            .WithDescription("""
                Ends the session and removes the session cookie. The request must send the `X-CSRF-Token` header.
                """)
            .ProducesError(ApiErrors.CsrfTokenInvalid)
            .RequireAuthorization(AuthServices.SessionPolicy);
    }

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
}
