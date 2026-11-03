using Dapper;
using Npgsql;

namespace BigFatFile.Server.Database;

/// <summary>
/// Starts a transaction in which row-level security shows the rows of one tenant and one user.
/// </summary>
internal static class TenantScope
{
    public static async Task<NpgsqlTransaction> BeginAsync(
        NpgsqlConnection connection, Guid? tenantId, Guid? userId, CancellationToken cancellationToken)
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
