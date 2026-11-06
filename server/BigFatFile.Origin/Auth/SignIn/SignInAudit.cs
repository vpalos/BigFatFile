using BigFatFile.Origin.Database;
using BigFatFile.Origin.Models;
using Npgsql;

namespace BigFatFile.Origin.Auth.SignIn;

/// <summary>The audit rows of sign-ins, with the client's IP address and user agent.</summary>
internal static class SignInAudit
{
    public const int MaxUserAgentLength = 512;

    /// <param name="tenantId">Null for a sign-in that reached no tenant, such as a failed one.</param>
    /// <param name="agentClientId">The App or the CLI that signed in for the user.</param>
    public static AuditModel.Entry Entry(
        HttpContext context,
        Guid? tenantId,
        string action,
        Guid userId,
        string? agentClientId = null,
        IReadOnlyDictionary<string, string>? details = null)
    {
        var address = context.Connection.RemoteIpAddress;
        if (address is { IsIPv4MappedToIPv6: true })
        {
            address = address.MapToIPv4();
        }

        var userAgent = context.Request.Headers.UserAgent.ToString();
        return new AuditModel.Entry(
            tenantId,
            action,
            userId,
            agentClientId,
            address,
            userAgent.Length == 0 ? null : userAgent[..Math.Min(userAgent.Length, MaxUserAgentLength)],
            details);
    }

    /// <summary>Writes the row in a transaction of its own, in the row's tenant.</summary>
    public static async Task WriteAsync(NpgsqlDataSource dataSource, AuditModel.Entry entry, CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await ScopedDatabase.BeginTenantTransactionAsync(connection, entry.TenantId, entry.ActorUserId, cancellationToken);
        await AuditModel.AddAsync(connection, entry, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }
}
