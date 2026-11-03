using BigFatFile.Contracts.Api;
using BigFatFile.Origin.Api.Errors;
using BigFatFile.Origin.Api.Parameters;
using BigFatFile.Origin.Auth.Tokens;
using Microsoft.AspNetCore.Http.HttpResults;

namespace BigFatFile.Origin.Api.Endpoints;

internal static class MemberEndpoints
{
    public static void MapMemberEndpoints(this IEndpointRouteBuilder tenant)
    {
        var members = tenant.MapGroup("/members");

        members.MapGet("", ListMembersAsync)
            .WithName("listMembers")
            .WithSummary("List members")
            .WithDescription("""
                Returns the tenant's members, ordered by name, one page at a time.
                """)
            .ProducesError(ApiErrors.NotFound)
            .RequireAuthorization(ScopeAuthorization.PolicyFor(BffScopes.MembersRead));

        members.MapPatch("/{userId:guid}", UpdateMemberAsync)
            .WithName("updateMember")
            .WithSummary("Change a member's role")
            .WithDescription("""
                Changes the role of one member of the tenant.
                """)
            .ProducesError(ApiErrors.AdminRequired)
            .ProducesError(ApiErrors.NotFound, "Here, the user is not a member of the tenant.")
            .ProducesError(ApiErrors.LastAdmin)
            .RequireAuthorization(ScopeAuthorization.PolicyFor(BffScopes.MembersManage));

        members.MapDelete("/{userId:guid}", RemoveMemberAsync)
            .WithName("removeMember")
            .WithSummary("Remove a member")
            .WithDescription("""
                Removes a user from the tenant. The user loses access at once, also through tokens issued earlier.
                """)
            .ProducesError(ApiErrors.AdminRequired)
            .ProducesError(ApiErrors.NotFound, "Here, the user is not a member of the tenant.")
            .ProducesError(ApiErrors.LastAdmin)
            .RequireAuthorization(ScopeAuthorization.PolicyFor(BffScopes.MembersManage));
    }

    private static Task<Results<Ok<Page<Member>>, ProblemHttpResult>> ListMembersAsync(
        Guid id, [AsParameters] PageQuery page, CancellationToken cancellationToken) =>
        throw new NotImplementedException();

    private static Task<Results<Ok<Member>, ProblemHttpResult>> UpdateMemberAsync(
        Guid id, Guid userId, UpdateMemberRequest request, CancellationToken cancellationToken) =>
        throw new NotImplementedException();

    private static Task<Results<NoContent, ProblemHttpResult>> RemoveMemberAsync(
        Guid id, Guid userId, CancellationToken cancellationToken) =>
        throw new NotImplementedException();
}
