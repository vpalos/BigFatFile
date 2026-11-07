using BigFatFile.Origin.Database;
using BigFatFile.Origin.Models;
using Microsoft.AspNetCore.Identity;
using Npgsql;

namespace BigFatFile.Origin.Auth;

/// <summary>
/// The audit rows of sign-ins and authorizations, with the client's IP address and user agent.
/// </summary>
internal static class AuthAudit
{
    public const string SignedIn = "signed_in";
    public const string AppAuthorized = "app_authorized";
    public const string SignInFailed = "sign_in_failed";

    /// <summary>
    /// A password sign-in, in the tenant that the sign-in chose.
    /// </summary>
    public static AuditModel.Entry SignedInEntry(HttpContext context, Guid tenantId, Guid userId) =>
        Entry(context, tenantId, SignedIn, userId);

    /// <summary>
    /// An App or the CLI received tokens to act for the user.
    /// </summary>
    public static AuditModel.Entry AppAuthorizedEntry(HttpContext context, Guid tenantId, Guid userId, string? clientId) =>
        Entry(context, tenantId, AppAuthorized, userId, agentClientId: clientId);

    /// <summary>
    /// A refused password sign-in. It has no tenant, so only the owner can read it.
    /// </summary>
    public static AuditModel.Entry SignInFailedEntry(HttpContext context, Guid userId, SignInResult result)
    {
        var reason = result switch
        {
            { IsLockedOut: true } => "locked_out",
            { IsNotAllowed: true } => "not_allowed",
            { RequiresTwoFactor: true } => "two_factor_required",
            _ => "wrong_password",
        };
        return Entry(context, tenantId: null, SignInFailed, userId, details: new Dictionary<string, string> { ["reason"] = reason });
    }

    /// <summary>
    /// Writes the row in a transaction of its own, in the row's tenant.
    /// </summary>
    public static async Task WriteAsync(NpgsqlDataSource dataSource, AuditModel.Entry entry, CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await ScopedDatabase.BeginTenantTransactionAsync(connection, entry.TenantId, entry.ActorUserId, cancellationToken);
        await AuditModel.AddAsync(connection, entry, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    private static AuditModel.Entry Entry(
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

        return new AuditModel.Entry(tenantId, action, userId, agentClientId, address, context.Request.Headers.UserAgent.ToString(), details);
    }
}
