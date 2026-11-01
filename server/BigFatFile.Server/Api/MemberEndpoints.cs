using BigFatFile.Contracts.Api;
using BigFatFile.Server.Auth;
using Microsoft.AspNetCore.Http.HttpResults;

namespace BigFatFile.Server.Api;

internal static class MemberEndpoints
{
    public static void MapMemberEndpoints(this IEndpointRouteBuilder tenant)
    {
        var members = tenant.MapGroup("/members");

        members.MapGet("", ListMembersAsync)
            .WithName("listMembers")
            .WithSummary("The tenant's members, ordered by name.")
            .ProducesProblem(StatusCodes.Status404NotFound)
            .RequireAuthorization(ScopeAuthorization.PolicyFor(BffScopes.MembersRead));

        members.MapPatch("/{userId:guid}", UpdateMemberAsync)
            .WithName("updateMember")
            .WithSummary("Changes a member's role. The tenant always keeps at least one admin.")
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .RequireAuthorization(ScopeAuthorization.PolicyFor(BffScopes.MembersManage));

        members.MapDelete("/{userId:guid}", RemoveMemberAsync)
            .WithName("removeMember")
            .WithSummary("Removes a member from the tenant. The tenant always keeps at least one admin.")
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
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
