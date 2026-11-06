using BigFatFile.Origin.Database;
using BigFatFile.Origin.Models;
using Dapper;
using Npgsql;

namespace BigFatFile.Origin.Jobs;

/// <summary>
/// Manages the audit log's monthly partitions:
/// - ensures the coming months exist, and...
/// - drops past months older than the retention.
/// </summary>
internal sealed class AuditPartitionsJob([FromKeyedServices(DatabaseServices.Owner)] NpgsqlDataSource owner)
{
    public static readonly TimeSpan Retention = TimeSpan.FromDays(90);
    public const int MonthsAhead = 2;

    /// <summary>
    /// Run this at startup, so that the partitions exist before the first request.
    /// </summary>
    public Task CreatePartitionsAsync(CancellationToken cancellationToken) =>
        InLockAsync(connection => AuditModel.CreatePartitionsAsync(connection, DateTimeOffset.UtcNow, MonthsAhead, cancellationToken), cancellationToken);

    public Task RunAsync(CancellationToken cancellationToken) =>
        InLockAsync(async connection =>
        {
            var now = DateTimeOffset.UtcNow;
            await AuditModel.CreatePartitionsAsync(connection, now, MonthsAhead, cancellationToken);
            await AuditModel.DropPartitionsAsync(connection, now - Retention, cancellationToken);
        }, cancellationToken);

    private async Task InLockAsync(Func<NpgsqlConnection, Task> work, CancellationToken cancellationToken)
    {
        await using var connection = await owner.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        // The advisory lock stops two nodes from creating the same partition at once.
        // The DDL locks the whole audit table, and every insert waits behind it. The timeout
        // makes the job fail instead of waiting long behind a slow query; Hangfire retries it.
        await connection.ExecuteAsync(new CommandDefinition(
            "SELECT pg_advisory_xact_lock(hashtext('audit_events partitions')); SET LOCAL lock_timeout = '5s';",
            cancellationToken: cancellationToken));
        await work(connection);
        await transaction.CommitAsync(cancellationToken);
    }
}
