using System.Security.Claims;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace BigFatFile.Origin.Auth;

/// <summary>
/// The tenant and the user that a session or a token works for.
/// </summary>
internal static class TenantClaims
{
    public const string TenantId = "tenant_id";

    public static Guid? TenantOf(ClaimsPrincipal? principal) =>
        Guid.TryParse(principal?.FindFirstValue(TenantId), out var id) ? id : null;

    /// <summary>
    /// The signed-in user.
    /// </summary>
    public static Guid? UserOf(ClaimsPrincipal? principal) =>
        Guid.TryParse(principal?.FindFirstValue(Claims.Subject), out var id) ? id : null;
}
