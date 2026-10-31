using System.Data;
using System.Data.Common;
using System.Diagnostics;
using Dapper;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace BigFatFile.Migrator;

/// <summary>
/// Applies pending migration files in order and records each one in <c>schema_migrations</c>.
/// </summary>
public sealed class SchemaMigrator(MigrationDialect dialect, ILogger? logger = null)
{
    private readonly ILogger logger = logger ?? NullLogger.Instance;

    /// <param name="files">
    /// The files of every source. Prefixes run in the order they first appear here, so pass
    /// the core's files first; within a prefix, files run by number.
    /// </param>
    public async Task<MigrationReport> RunAsync(
        DbConnection connection, IEnumerable<MigrationFile> files, CancellationToken cancellationToken = default)
    {
        var ordered = Order(files);
        if (connection.State != ConnectionState.Open)
        {
            await connection.OpenAsync(cancellationToken);
        }

        await using var runLock = await dialect.LockRunAsync(
            connection,
            () => logger.LogInformation("Waiting for another process to finish its migrations"),
            cancellationToken);

        await connection.ExecuteAsync(new CommandDefinition(dialect.CreateTableSql, cancellationToken: cancellationToken));
        var applied = (await connection.QueryAsync<AppliedRow>(new CommandDefinition(
                "SELECT prefix, number, name, checksum FROM schema_migrations", cancellationToken: cancellationToken)))
            .ToDictionary(row => (row.Prefix, row.Number));

        var pending = FindPending(ordered, applied);
        var done = new List<MigrationFile>();
        foreach (var file in pending)
        {
            if (await ApplyAsync(connection, file, cancellationToken))
            {
                done.Add(file);
            }
        }

        if (done.Count == 0)
        {
            logger.LogInformation("Database schema is up to date ({Count} migrations)", ordered.Count);
        }

        return new MigrationReport(done, ordered.Count - pending.Count);
    }

    private static List<MigrationFile> Order(IEnumerable<MigrationFile> files)
    {
        var list = files.ToList();
        var duplicate = list.GroupBy(f => (f.Prefix, f.Number)).FirstOrDefault(g => g.Count() > 1);
        if (duplicate is not null)
        {
            throw new MigrationException(
                $"Two migrations share a number: {string.Join(" and ", duplicate.Select(f => f.FileName))}.");
        }

        var prefixOrder = list.Select(f => f.Prefix).Distinct().Select((prefix, index) => (prefix, index))
            .ToDictionary(p => p.prefix, p => p.index);
        return list.OrderBy(f => prefixOrder[f.Prefix]).ThenBy(f => f.Number).ToList();
    }

    private static List<MigrationFile> FindPending(
        List<MigrationFile> ordered, Dictionary<(string Prefix, int Number), AppliedRow> applied)
    {
        foreach (var file in ordered)
        {
            if (!applied.TryGetValue((file.Prefix, file.Number), out var row))
            {
                continue;
            }

            if (row.Name != file.Name)
            {
                throw new MigrationException(
                    $"{file.FileName} reuses a number: the database applied {row.Prefix}_{row.Number:D4}_{row.Name}.sql under it.");
            }

            if (row.Checksum != file.Checksum)
            {
                throw new MigrationException(
                    $"{file.FileName} has changed since it was applied. Applied migrations must not be edited; add a new file instead.");
            }
        }

        var pending = ordered.Where(f => !applied.ContainsKey((f.Prefix, f.Number))).ToList();
        foreach (var file in pending)
        {
            var later = applied.Values.Where(r => r.Prefix == file.Prefix && r.Number > file.Number).MinBy(r => r.Number);
            if (later is not null)
            {
                throw new MigrationException(
                    $"{file.FileName} is not applied, but the later {later.Prefix}_{later.Number:D4}_{later.Name}.sql is. Give it a number after {later.Number:D4}.");
            }
        }

        return pending;
    }

    private async Task<bool> ApplyAsync(DbConnection connection, MigrationFile file, CancellationToken cancellationToken)
    {
        var record = new { file.Prefix, file.Number, file.Name, file.Checksum };
        var started = Stopwatch.GetTimestamp();
        logger.LogInformation("Applying {File}", file.FileName);

        try
        {
            if (!file.RunsInTransaction)
            {
                await connection.ExecuteAsync(new CommandDefinition(file.Sql, commandTimeout: 0, cancellationToken: cancellationToken));
                await connection.ExecuteAsync(new CommandDefinition(dialect.InsertSql, record, cancellationToken: cancellationToken));
            }
            else
            {
                await using var transaction = await dialect.BeginAsync(connection, cancellationToken);

                // Another process may have applied the file since the table was read. This matters
                // on SQLite, where only this transaction holds the write lock.
                var exists = await connection.ExecuteScalarAsync<long>(new CommandDefinition(
                    "SELECT COUNT(*) FROM schema_migrations WHERE prefix = @Prefix AND number = @Number",
                    record, transaction.Transaction, cancellationToken: cancellationToken));
                if (exists > 0)
                {
                    return false;
                }

                await connection.ExecuteAsync(new CommandDefinition(
                    file.Sql, transaction: transaction.Transaction, commandTimeout: 0, cancellationToken: cancellationToken));
                await connection.ExecuteAsync(new CommandDefinition(
                    dialect.InsertSql, record, transaction.Transaction, cancellationToken: cancellationToken));
                await transaction.CommitAsync(cancellationToken);
            }
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            throw new MigrationException($"{file.FileName} failed: {exception.Message}", exception);
        }

        logger.LogInformation("Applied {File} in {Milliseconds} ms", file.FileName, Stopwatch.GetElapsedTime(started).TotalMilliseconds);
        return true;
    }

    private sealed class AppliedRow
    {
        public string Prefix { get; init; } = "";

        public int Number { get; init; }

        public string Name { get; init; } = "";

        public string Checksum { get; init; } = "";
    }
}
