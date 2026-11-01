using BigFatFile.Contracts.Api;
using BigFatFile.Server.Auth;
using Microsoft.AspNetCore.Http.HttpResults;

namespace BigFatFile.Server.Api;

internal static class InvitationEndpoints
{
    public static void MapInvitationEndpoints(this IEndpointRouteBuilder tenant)
    {
        var invitations = tenant.MapGroup("/invitations");

        invitations.MapGet("", ListInvitationsAsync)
            .WithName("listInvitations")
            .WithSummary("The invitations nobody has accepted yet, newest first.")
            .ProducesProblem(StatusCodes.Status404NotFound)
            .RequireAuthorization(ScopeAuthorization.PolicyFor(BffScopes.MembersManage));

        invitations.MapPost("", CreateInvitationAsync)
            .WithName("createInvitation")
            .WithSummary("Invites an email address and sends the invitation email. Inviting the same address again replaces the earlier invitation and sends a new email.")
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .RequireAuthorization(ScopeAuthorization.PolicyFor(BffScopes.MembersManage));

        invitations.MapDelete("/{invitationId:guid}", RevokeInvitationAsync)
            .WithName("revokeInvitation")
            .WithSummary("Revokes an invitation, so its email link stops working.")
            .ProducesProblem(StatusCodes.Status404NotFound)
            .RequireAuthorization(ScopeAuthorization.PolicyFor(BffScopes.MembersManage));
    }

    private static Task<Results<Ok<Page<Invitation>>, ProblemHttpResult>> ListInvitationsAsync(
        Guid id, [AsParameters] PageQuery page, CancellationToken cancellationToken) =>
        throw new NotImplementedException();

    private static Task<Results<Created<Invitation>, ProblemHttpResult>> CreateInvitationAsync(
        Guid id, CreateInvitationRequest request, CancellationToken cancellationToken) =>
        throw new NotImplementedException();

    private static Task<Results<NoContent, ProblemHttpResult>> RevokeInvitationAsync(
        Guid id, Guid invitationId, CancellationToken cancellationToken) =>
        throw new NotImplementedException();
}
