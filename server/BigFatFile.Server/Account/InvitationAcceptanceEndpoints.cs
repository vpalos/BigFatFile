using BigFatFile.Contracts.Account;
using BigFatFile.Server.Api;
using BigFatFile.Server.Auth;
using Microsoft.AspNetCore.Http.HttpResults;

namespace BigFatFile.Server.Account;

internal static class InvitationAcceptanceEndpoints
{
    public static void MapInvitationAcceptanceEndpoints(this IEndpointRouteBuilder account)
    {
        var invitations = account.MapGroup("/invitations");

        invitations.MapPost("/lookup", LookUpAsync)
            .WithName("lookUpInvitation")
            .WithSummary("Look up an invitation")
            .WithDescription("""
                Returns what an invitation is for, so that the invitation screen can show it before the person accepts: the tenant, who sent it, the invited address and the role.

                - `accountExists` tells the screen whether to ask the person to sign in or to sign up.
                - The token is sent in the request body, so it stays out of request logs.
                """)
            .ProducesError(ApiErrors.NotFound, "Here, the token is unknown, has expired, was revoked or was already accepted.")
            .AllowAnonymous();

        invitations.MapPost("/accept", AcceptAsync)
            .WithName("acceptInvitation")
            .WithSummary("Accept an invitation")
            .WithDescription("""
                Adds the signed-in user to the invited tenant, with the invited role. The session then works in that tenant.
                """)
            .ProducesError(ApiErrors.NotFound, "Here, the token is unknown, has expired, was revoked or was already accepted.")
            .ProducesError(ApiErrors.WrongAccount)
            .RequireAuthorization(AuthServices.SessionPolicy);
    }

    private static Task<Results<Ok<InvitationPreview>, ProblemHttpResult>> LookUpAsync(InvitationTokenRequest request, CancellationToken cancellationToken) =>
        throw new NotImplementedException();

    private static Task<Results<NoContent, ProblemHttpResult>> AcceptAsync(InvitationTokenRequest request, HttpContext context) =>
        throw new NotImplementedException();
}
