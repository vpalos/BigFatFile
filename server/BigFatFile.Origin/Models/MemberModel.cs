using BigFatFile.Contracts.Api;
using Dapper;
using Npgsql;

namespace BigFatFile.Origin.Models;

/// <summary>The SQL for tenant memberships, the <c>tenant_members</c> table.</summary>
internal static class MemberModel
{
    public sealed record UserTenant(Guid TenantId, string TenantName, TenantRole Role);

    /// <summary>The user's tenants, with the user's role in each, ordered by name.</summary>
    public static async Task<IReadOnlyList<UserTenant>> ListTenantsOfUserAsync(
        NpgsqlConnection connection, Guid userId, CancellationToken cancellationToken)
    {
        var rows = await connection.QueryAsync<(Guid TenantId, string TenantName, string Role)>(new CommandDefinition(
            """
            SELECT t.id, t.name, m.role
            FROM tenant_members m
            JOIN tenants t ON t.id = m.tenant_id
            WHERE m.user_id = @UserId
            ORDER BY t.name
            """,
            new { UserId = userId },
            cancellationToken: cancellationToken));
        return [.. rows.Select(row => new UserTenant(row.TenantId, row.TenantName, Enum.Parse<TenantRole>(row.Role, ignoreCase: true)))];
    }

    public static Task<bool> ExistsAsync(NpgsqlConnection connection, Guid tenantId, Guid userId, CancellationToken cancellationToken) =>
        connection.ExecuteScalarAsync<bool>(new CommandDefinition(
            "SELECT EXISTS (SELECT 1 FROM tenant_members WHERE tenant_id = @TenantId AND user_id = @UserId)",
            new { TenantId = tenantId, UserId = userId },
            cancellationToken: cancellationToken));

    /// <returns>The tenant that the user signed in to last, or else the oldest membership; null when the user is a member of none.</returns>
    public static Task<Guid?> LastSignedInTenantAsync(NpgsqlConnection connection, Guid userId, CancellationToken cancellationToken) =>
        connection.QueryFirstOrDefaultAsync<Guid?>(new CommandDefinition(
            """
            SELECT tenant_id FROM tenant_members
            WHERE user_id = @UserId
            ORDER BY last_signed_in_at DESC NULLS LAST, created_at
            LIMIT 1
            """,
            new { UserId = userId },
            cancellationToken: cancellationToken));

    public static Task MarkSignedInAsync(NpgsqlConnection connection, Guid tenantId, Guid userId, CancellationToken cancellationToken) =>
        connection.ExecuteAsync(new CommandDefinition(
            "UPDATE tenant_members SET last_signed_in_at = now() WHERE tenant_id = @TenantId AND user_id = @UserId",
            new { TenantId = tenantId, UserId = userId },
            cancellationToken: cancellationToken));

    public static Task AddAsync(NpgsqlConnection connection, Guid tenantId, Guid userId, TenantRole role, CancellationToken cancellationToken) =>
        connection.ExecuteAsync(new CommandDefinition(
            "INSERT INTO tenant_members (tenant_id, user_id, role) VALUES (@TenantId, @UserId, @Role)",
            new { TenantId = tenantId, UserId = userId, Role = role.ToString().ToLowerInvariant() },
            cancellationToken: cancellationToken));
}
