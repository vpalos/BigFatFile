using System.ComponentModel;
using BigFatFile.Contracts.Api;
using BigFatFile.Origin.Api.Errors;
using BigFatFile.Origin.Api.Parameters;
using BigFatFile.Origin.Auth.Tokens;
using BigFatFile.Origin.Database;
using BigFatFile.Origin.Jobs;
using BigFatFile.Origin.Models;
using Microsoft.AspNetCore.Http.HttpResults;

namespace BigFatFile.Origin.Api.Endpoints;

internal static class AuditEndpoints
{
    public static void MapAuditEndpoints(this IEndpointRouteBuilder tenant) =>
        tenant.MapGet("/audit", ListAuditEventsAsync)
            .WithName("listAuditEvents")
            .WithSummary("List audit events")
            .WithDescription($"""
                Returns the tenant's audit log, newest first, one page at a time: who signed in, and which Apps received tokens to act for a user.

                Events are kept for at least {AuditPartitionsJob.Retention.TotalDays} days.
                """)
            .ProducesError(ApiErrors.BadRequest, "Here, the cursor is not one that this endpoint returned, or the limit is out of range.")
            .ProducesError(ApiErrors.AdminRequired)
            .ProducesError(ApiErrors.NotFound)
            .RequireAuthorization(ScopeAuthorization.PolicyFor(BffScopes.TenantAdmin));

    private static async Task<Results<Ok<Page<AuditEvent>>, ProblemHttpResult>> ListAuditEventsAsync(
        Guid id,
        [Description("Only events at this time or later.")] DateTimeOffset? from,
        [Description("Only events before this time.")] DateTimeOffset? to,
        [Description("Only events by this user.")] Guid? actor,
        [AsParameters] PageQuery page,
        ScopedDatabase database,
        CancellationToken cancellationToken)
    {
        (DateTimeOffset, Guid)? after = null;
        if (page.Cursor is not null)
        {
            if (!TimeCursor.TryDecode(page.Cursor, out var position))
            {
                return Problems.Of(ApiErrors.BadRequest, "The cursor is not valid.");
            }

            after = position;
        }

        var limit = page.Limit ?? PageQuery.DefaultLimit;
        if (limit is < 1 or > PageQuery.MaxLimit)
        {
            return Problems.Of(ApiErrors.BadRequest, $"The limit must be between 1 and {PageQuery.MaxLimit}.");
        }

        var connection = await database.OpenAsync(cancellationToken);

        // One row more than the page shows whether there is a next page.
        var rows = await AuditModel.ListAsync(connection, id, from, to, actor, after, limit + 1, cancellationToken);
        var next = rows.Count > limit ? TimeCursor.Encode(rows[limit - 1].OccurredAt, rows[limit - 1].Id) : null;
        return TypedResults.Ok(new Page<AuditEvent>([.. rows.Take(limit).Select(ToApi)], next));
    }

    private static AuditEvent ToApi(AuditModel.Row row) => new(
        row.Id,
        row.OccurredAt,
        row.Action switch
        {
            AuditModel.SignedIn => AuditAction.SignedIn,
            AuditModel.AppAuthorized => AuditAction.AppAuthorized,
            _ => throw new InvalidOperationException($"The audit action {row.Action} has no value in {nameof(AuditAction)}."),
        },
        row.ActorUserId is { } userId ? new AuditActor(userId, row.ActorEmail, row.ActorName) : null,
        row.AgentClientId,
        row.IpAddress,
        row.UserAgent);
}
