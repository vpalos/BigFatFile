using System.Globalization;
using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using Dapper;
using Npgsql;

namespace BigFatFile.Origin.Models;

/// <summary>
/// The SQL for the audit log: the <c>audit_events</c> table and its monthly partitions.
/// </summary>
internal static partial class AuditModel
{
    public const string SignedIn = "signed_in";
    public const string AppAuthorized = "app_authorized";
    public const string SignInFailed = "sign_in_failed";

    /// <param name="TenantId">Null for an event outside any tenant, which only the owner can read.</param>
    /// <param name="AgentClientId">The App or the CLI that acted for the user.</param>
    public sealed record Entry(
        Guid? TenantId,
        string Action,
        Guid? ActorUserId,
        string? AgentClientId,
        IPAddress? IpAddress,
        string? UserAgent,
        IReadOnlyDictionary<string, string>? Details = null);

    /// <param name="ActorEmail">Null once the user is deleted.</param>
    public sealed record Row(
        Guid Id,
        DateTimeOffset OccurredAt,
        string Action,
        Guid? ActorUserId,
        string? ActorEmail,
        string? ActorName,
        string? AgentClientId,
        string? IpAddress,
        string? UserAgent);

    public static Task AddAsync(NpgsqlConnection connection, Entry entry, CancellationToken cancellationToken) =>
        connection.ExecuteAsync(new CommandDefinition(
            """
            INSERT INTO audit_events (tenant_id, action, actor_user_id, agent_client_id, ip_address, user_agent, details)
            VALUES (@TenantId, @Action, @ActorUserId, @AgentClientId, @IpAddress::inet, @UserAgent, @Details::jsonb)
            """,
            new
            {
                entry.TenantId,
                entry.Action,
                entry.ActorUserId,
                entry.AgentClientId,
                IpAddress = entry.IpAddress?.ToString(),
                entry.UserAgent,
                Details = entry.Details is null ? null : JsonSerializer.Serialize(entry.Details),
            },
            cancellationToken: cancellationToken));

    /// <summary>The tenant's events, newest first.</summary>
    /// <param name="from">Inclusive.</param>
    /// <param name="to">Exclusive.</param>
    /// <param name="after">The time and the ID of the last row of the previous page.</param>
    public static async Task<IReadOnlyList<Row>> ListAsync(
        NpgsqlConnection connection,
        Guid tenantId,
        DateTimeOffset? from,
        DateTimeOffset? to,
        Guid? actorUserId,
        (DateTimeOffset OccurredAt, Guid Id)? after,
        int limit,
        CancellationToken cancellationToken)
    {
        var rows = await connection.QueryAsync<ListedRow>(new CommandDefinition(
            """
            SELECT e.id AS Id, e.occurred_at AS OccurredAt, e.action AS Action, e.actor_user_id AS ActorUserId,
                   u.email AS ActorEmail, u.user_name AS ActorName, e.agent_client_id AS AgentClientId,
                   host(e.ip_address) AS IpAddress, e.user_agent AS UserAgent
            FROM audit_events e
            LEFT JOIN users u ON u.id = e.actor_user_id
            WHERE e.tenant_id = @TenantId
              AND (@From::timestamptz IS NULL OR e.occurred_at >= @From)
              AND (@To::timestamptz IS NULL OR e.occurred_at < @To)
              AND (@ActorUserId::uuid IS NULL OR e.actor_user_id = @ActorUserId)
              AND (@AfterAt::timestamptz IS NULL OR (e.occurred_at, e.id) < (@AfterAt, @AfterId))
            ORDER BY e.occurred_at DESC, e.id DESC
            LIMIT @Limit
            """,
            new
            {
                TenantId = tenantId,
                From = from?.ToUniversalTime(),
                To = to?.ToUniversalTime(),
                ActorUserId = actorUserId,
                AfterAt = after?.OccurredAt.ToUniversalTime(),
                AfterId = after?.Id,
                Limit = limit,
            },
            cancellationToken: cancellationToken));
        return [.. rows.Select(row => new Row(
            row.Id,
            new DateTimeOffset(row.OccurredAt, TimeSpan.Zero),
            row.Action,
            row.ActorUserId,
            row.ActorEmail,
            row.ActorName,
            row.AgentClientId,
            row.IpAddress,
            row.UserAgent))];
    }

    /// <summary>
    /// Creates the partitions that are missing, one per month, from the month of <paramref name="now"/>
    /// to <paramref name="monthsAhead"/> months later. Months start at midnight UTC.
    /// </summary>
    public static async Task CreatePartitionsAsync(NpgsqlConnection connection, DateTimeOffset now, int monthsAhead, CancellationToken cancellationToken)
    {
        var existing = (await PartitionsAsync(connection, cancellationToken)).Select(partition => partition.Name).ToHashSet(StringComparer.Ordinal);
        var utc = now.UtcDateTime;
        var first = new DateTimeOffset(utc.Year, utc.Month, 1, 0, 0, 0, TimeSpan.Zero);
        for (var month = 0; month <= monthsAhead; month++)
        {
            var start = first.AddMonths(month);
            var name = string.Create(CultureInfo.InvariantCulture, $"audit_events_{start:yyyy_MM}");
            if (existing.Contains(name))
            {
                continue;
            }

            await connection.ExecuteAsync(new CommandDefinition(
                $"CREATE TABLE {name} PARTITION OF audit_events FOR VALUES FROM ('{Literal(start)}') TO ('{Literal(start.AddMonths(1))}')",
                cancellationToken: cancellationToken));
        }
    }

    /// <summary>Drops the monthly partitions whose last row is older than <paramref name="cutoff"/>.</summary>
    public static async Task DropPartitionsAsync(NpgsqlConnection connection, DateTimeOffset cutoff, CancellationToken cancellationToken)
    {
        foreach (var (name, start) in await PartitionsAsync(connection, cancellationToken))
        {
            if (start.AddMonths(1) <= cutoff)
            {
                await connection.ExecuteAsync(new CommandDefinition($"DROP TABLE {name}", cancellationToken: cancellationToken));
            }
        }
    }

    /// <summary>
    /// Returns the list of audit log partitions named by month.
    /// Any other partition is left alone.
    /// </summary>
    private static async Task<IReadOnlyList<(string Name, DateTimeOffset Start)>> PartitionsAsync(
        NpgsqlConnection connection, CancellationToken cancellationToken)
    {
        var names = await connection.QueryAsync<string>(new CommandDefinition(
            """
            SELECT c.relname FROM pg_inherits i JOIN pg_class c ON c.oid = i.inhrelid
            WHERE i.inhparent = 'audit_events'::regclass
            """,
            cancellationToken: cancellationToken));
        return [.. names
            .Select(name => (Name: name, Match: MonthlyPartition().Match(name)))
            .Where(partition => partition.Match.Success)
            .Select(partition => (partition.Name, new DateTimeOffset(
                int.Parse(partition.Match.Groups[1].Value, CultureInfo.InvariantCulture),
                int.Parse(partition.Match.Groups[2].Value, CultureInfo.InvariantCulture),
                1, 0, 0, 0, TimeSpan.Zero)))];
    }

    private static string Literal(DateTimeOffset time) => time.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture);

    [GeneratedRegex(@"^audit_events_(\d{4})_(0[1-9]|1[0-2])$")]
    private static partial Regex MonthlyPartition();

    private sealed record ListedRow(
        Guid Id,
        DateTime OccurredAt,
        string Action,
        Guid? ActorUserId,
        string? ActorEmail,
        string? ActorName,
        string? AgentClientId,
        string? IpAddress,
        string? UserAgent);
}
