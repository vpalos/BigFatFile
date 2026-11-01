using BigFatFile.Contracts.Account;
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
            .WithSummary("What an invitation is for, before the person accepts it.")
            .ProducesProblem(StatusCodes.Status404NotFound)
            .AllowAnonymous();

        invitations.MapPost("/accept", AcceptAsync)
            .WithName("acceptInvitation")
            .WithSummary("Joins the tenant and scopes the session to it. The session's user must have the invited address.")
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .RequireAuthorization(AuthServices.SessionPolicy);
    }

    private static Task<Results<Ok<InvitationPreview>, ProblemHttpResult>> LookUpAsync(InvitationTokenRequest request, CancellationToken cancellationToken) =>
        throw new NotImplementedException();

    private static Task<Results<NoContent, ProblemHttpResult>> AcceptAsync(InvitationTokenRequest request, HttpContext context) =>
        throw new NotImplementedException();
}
