using System.Data.Common;
using Dapper;

namespace BigFatFile.Migrator;

/// <summary>
/// A transaction opened either through ADO.NET or with plain SQL. <see cref="Transaction"/>
/// is null in the second case, and commands then run without one.
/// </summary>
internal sealed class MigrationTransaction(DbConnection connection, DbTransaction? transaction) : IAsyncDisposable
{
    private bool finished;

    public DbTransaction? Transaction => transaction;

    public async Task CommitAsync(CancellationToken cancellationToken)
    {
        if (transaction is not null)
        {
            await transaction.CommitAsync(cancellationToken);
        }
        else
        {
            await connection.ExecuteAsync(new CommandDefinition("COMMIT", cancellationToken: cancellationToken));
        }

        finished = true;
    }

    public async ValueTask DisposeAsync()
    {
        if (!finished)
        {
            if (transaction is not null)
            {
                await transaction.RollbackAsync();
            }
            else
            {
                try
                {
                    await connection.ExecuteAsync("ROLLBACK");
                }
                catch (DbException)
                {
                    // SQLite rolls back by itself after some errors, and then ROLLBACK fails.
                    // The error that caused the rollback is already on its way to the caller.
                }
            }
        }

        if (transaction is not null)
        {
            await transaction.DisposeAsync();
        }
    }
}
