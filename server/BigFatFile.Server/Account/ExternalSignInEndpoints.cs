using BigFatFile.Server.Api;
using Microsoft.AspNetCore.Http.HttpResults;

namespace BigFatFile.Server.Account;

internal static class ExternalSignInEndpoints
{
    public static void MapExternalSignInEndpoints(this IEndpointRouteBuilder account) =>
        account.MapGet("/external/{provider}", StartAsync)
            .WithName("signInExternally")
            .WithSummary("Sign in with an external account")
            .WithDescription("""
                Sends the browser to the sign-in page of another service, such as Google or Microsoft. When the person has signed in there, the browser comes back to `returnUrl`, which must be a path on this server.

                - `provider` is a `key` from `GET /account/options`.
                - The web app opens this address as a page in the browser, not with a fetch call, because the browser must follow the redirects.
                """)
            .Produces(StatusCodes.Status302Found)
            .ProducesError(ApiErrors.NotFound, "Here, this server has no provider with that key.")
            .AllowAnonymous();

    private static Task<Results<ChallengeHttpResult, ProblemHttpResult>> StartAsync(string provider, string? returnUrl, HttpContext context) =>
        throw new NotImplementedException();
}
