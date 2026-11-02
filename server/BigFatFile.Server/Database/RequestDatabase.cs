using BigFatFile.Server.Auth;
using Npgsql;

namespace BigFatFile.Server.Database;

/// <summary>
/// The request's database connection. Its transaction works for the request's tenant and
/// user, so row-level security shows only their rows. Nothing is saved until a handler
/// calls <see cref="CommitAsync"/>.
/// </summary>
internal sealed class RequestDatabase(NpgsqlDataSource dataSource, IHttpContextAccessor http) : IAsyncDisposable
{
    private NpgsqlConnection? connection;
    private NpgsqlTransaction? transaction;

    public async ValueTask<NpgsqlConnection> OpenAsync(CancellationToken cancellationToken)
    {
        if (connection is null)
        {
            var principal = http.HttpContext?.User;
            connection = await dataSource.OpenConnectionAsync(cancellationToken);
            transaction = await TenantScope.BeginAsync(
                connection, TenantClaims.TenantOf(principal), TenantClaims.UserOf(principal), cancellationToken);
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
}
