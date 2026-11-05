using BigFatFile.Origin.Database;
using BigFatFile.Origin.Models;
using Npgsql;

namespace BigFatFile.Origin.Auth.SignIn;

/// <summary>Chooses the tenant that a new session works in, and records the choice.</summary>
internal static class SignInTenant
{
    /// <returns>The tenant that the user signed in to last, or else the oldest membership; null when the user is a member of none.</returns>
    public static async Task<Guid?> ChooseAsync(NpgsqlDataSource dataSource, Guid userId, CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        Guid? tenantId;
        await using (await ScopedDatabase.BeginTenantTransactionAsync(connection, tenantId: null, userId, cancellationToken))
        {
            tenantId = await MemberModel.LastSignedInTenantAsync(connection, userId, cancellationToken);
        }

        if (tenantId is null)
        {
            return null;
        }

        // A membership changes only inside its own tenant, so the record is a second transaction.
        await using var transaction = await ScopedDatabase.BeginTenantTransactionAsync(connection, tenantId, userId, cancellationToken);
        await MemberModel.MarkSignedInAsync(connection, tenantId.Value, userId, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return tenantId;
    }
}
