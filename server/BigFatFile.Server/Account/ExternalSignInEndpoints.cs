using Microsoft.AspNetCore.Http.HttpResults;

namespace BigFatFile.Server.Account;

internal static class ExternalSignInEndpoints
{
    public static void MapExternalSignInEndpoints(this IEndpointRouteBuilder account) =>
        account.MapGet("/external/{provider}", StartAsync)
            .WithName("signInExternally")
            .WithSummary("Sends the browser to the provider's sign-in page. After it, the browser returns to returnUrl, which must be a path on this server.")
            .Produces(StatusCodes.Status302Found)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .AllowAnonymous();

    private static Task<Results<ChallengeHttpResult, ProblemHttpResult>> StartAsync(string provider, string? returnUrl, HttpContext context) =>
        throw new NotImplementedException();
}
