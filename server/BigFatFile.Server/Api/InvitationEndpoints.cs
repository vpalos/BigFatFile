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
            .WithSummary("List invitations")
            .WithDescription("""
                Returns the invitations that nobody has accepted yet, newest first, one page at a time.

                It needs the `members.manage` scope, because the invited addresses belong to people who are not members yet.
                """)
            .ProducesError(ApiErrors.AdminRequired)
            .ProducesError(ApiErrors.NotFound)
            .RequireAuthorization(ScopeAuthorization.PolicyFor(BffScopes.MembersManage));

        invitations.MapPost("", CreateInvitationAsync)
            .WithName("createInvitation")
            .WithSummary("Invite someone")
            .WithDescription("""
                Invites an email address to the tenant with a role, and sends the invitation email. The link in the email works until `expiresAt`.

                Inviting the same address again replaces the earlier invitation and sends a new email.
                """)
            .ProducesError(ApiErrors.InvalidFields)
            .ProducesError(ApiErrors.AdminRequired)
            .ProducesError(ApiErrors.NotFound)
            .ProducesError(ApiErrors.AlreadyMember)
            .RequireAuthorization(ScopeAuthorization.PolicyFor(BffScopes.MembersManage));

        invitations.MapDelete("/{invitationId:guid}", RevokeInvitationAsync)
            .WithName("revokeInvitation")
            .WithSummary("Revoke an invitation")
            .WithDescription("""
                Revokes an invitation. The link in its email stops working.
                """)
            .ProducesError(ApiErrors.AdminRequired)
            .ProducesError(ApiErrors.NotFound, "Here, the invitation does not exist or was already accepted.")
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
