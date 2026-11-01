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
            .WithSummary("Whether this server accepts sign-ups without an invitation, and the external accounts it accepts.")
            .WithDescription("This is the description.")
            .WithDisplayName("AUTHOptions")
            .AllowAnonymous();

        account.MapGet("/csrf", GetCsrfToken)
            .WithName("getCsrfToken")
            .WithSummary("The token that writes made with the session cookie send in the X-CSRF-Token header.")
            .AllowAnonymous();

        account.MapPost("/sign-in", SignInAsync)
            .WithName("signIn")
            .WithSummary("Starts a session with an email and a password.")
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .AllowAnonymous();

        account.MapPost("/sign-out", SignOutAsync)
            .WithName("signOut")
            .WithSummary("Ends the session.")
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .RequireAuthorization(AuthServices.SessionPolicy);

        account.MapPut("/tenant", SwitchTenantAsync)
            .WithName("switchTenant")
            .WithSummary("Scopes the session to another of the user's tenants.")
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
