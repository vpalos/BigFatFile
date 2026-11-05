using BigFatFile.Origin.Auth;
using Dapper;
using Npgsql;

namespace BigFatFile.Origin.Database;

/// <summary>
/// The request's database connection. Its transaction works for the request's tenant and
/// user, so row-level security shows only their rows. Nothing is saved until a handler
/// calls <see cref="CommitAsync"/>.
/// </summary>
internal sealed class ScopedDatabase(NpgsqlDataSource dataSource, IHttpContextAccessor http) : IAsyncDisposable
{
    private NpgsqlConnection? connection;
    private NpgsqlTransaction? transaction;

    public async ValueTask<NpgsqlConnection> OpenAsync(CancellationToken cancellationToken)
    {
        if (connection is null)
        {
            var principal = http.HttpContext?.User;
            connection = await dataSource.OpenConnectionAsync(cancellationToken);
            transaction = await BeginTenantTransactionAsync(
                connection,
                TenantClaims.TenantOf(principal),
                TenantClaims.UserOf(principal),
                cancellationToken);
        }

        return connection;
    }

    public async Task CommitAsync(CancellationToken cancellationToken)
    {
        if (transaction is not null)
        {
            await transaction.CommitAsync(cancellationToken);
        }
    }

    public async ValueTask DisposeAsync()
    {
        // Disposing a transaction that was not committed rolls it back.
        if (transaction is not null)
        {
            await transaction.DisposeAsync();
        }

        if (connection is not null)
        {
            await connection.DisposeAsync();
        }
    }

    /// <summary>
    /// Starts a transaction under the Tenant's RLS rules.
    /// </summary>
    public static async Task<NpgsqlTransaction> BeginTenantTransactionAsync(
        NpgsqlConnection connection,
        Guid? tenantId,
        Guid? userId,
        CancellationToken cancellationToken)
    {
        var transaction = await connection.BeginTransactionAsync(cancellationToken);

        await connection.ExecuteAsync(new CommandDefinition(
            "SELECT set_config('app.tenant_id', @TenantId, true), set_config('app.user_id', @UserId, true)",
            new
            {
                TenantId = tenantId?.ToString() ?? "",
                UserId = userId?.ToString() ?? ""
            },
            transaction,
            cancellationToken: cancellationToken));

        return transaction;
    }
}
