using System.Security.Claims;
using BigFatFile.Origin.Auth.Users;
using Microsoft.AspNetCore.Identity;
using Microsoft.IdentityModel.Tokens;
using OpenIddict.Abstractions;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace BigFatFile.Origin.Auth.Tokens;

/// <summary>
/// The claims that the token endpoints put into the tokens they issue.
/// </summary>
internal static class TokenIdentity
{
    public static async Task<ClaimsPrincipal> ForUserAsync(
        UserManager<BffUser> users,
        BffUser user,
        string? tenantId,
        IEnumerable<string> scopes,
        IEnumerable<string> resources)
    {
        var identity = NewIdentity();
        identity.SetClaim(Claims.Subject, user.Id.ToString())
            .SetClaim(Claims.Email, user.Email)
            .SetClaim(Claims.Name, user.UserName)
            .SetClaims(Claims.Role, [.. await users.GetRolesAsync(user)])
            .SetClaim(TenantClaims.TenantId, tenantId);
        return Finish(identity, scopes, resources);
    }

    private static ClaimsIdentity NewIdentity() =>
        new(TokenValidationParameters.DefaultAuthenticationType, Claims.Name, Claims.Role);

    private static ClaimsPrincipal Finish(ClaimsIdentity identity, IEnumerable<string> scopes, IEnumerable<string> resources)
    {
        identity.SetScopes(scopes);
        identity.SetResources(resources);
        identity.SetDestinations(DestinationsOf);
        return new ClaimsPrincipal(identity);
    }

    private static IEnumerable<string> DestinationsOf(Claim claim) => claim.Type switch
    {
        Claims.Subject => [Destinations.AccessToken, Destinations.IdentityToken],
        Claims.Name when claim.Subject!.HasScope(BffScopes.Profile) => [Destinations.AccessToken, Destinations.IdentityToken],
        Claims.Email when claim.Subject!.HasScope(BffScopes.Email) => [Destinations.AccessToken, Destinations.IdentityToken],
        _ => [Destinations.AccessToken],
    };
}
