using System.Security.Claims;
using BigFatFile.Origin.Auth.Users;
using Microsoft.AspNetCore.Identity;
using Npgsql;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace BigFatFile.Origin.Auth.SignIn;

/// <summary>Signs a user in with a password. Every form and endpoint that takes a password calls this.</summary>
internal static class PasswordSignIn
{
    /// <summary>Checks the password, then starts a session in the tenant that <see cref="SignInTenant"/> chooses.</summary>
    public static async Task<SignInResult> SignInAsync(
        SignInManager<BffUser> signIn, NpgsqlDataSource dataSource, BffUser user, string password, CancellationToken cancellationToken)
    {
        var result = await signIn.CheckPasswordSignInAsync(user, password, lockoutOnFailure: true);
        if (!result.Succeeded)
        {
            return result;
        }

        // The password check ignores two-factor sign-in, and this server has no second step yet.
        // Without this refusal, such an account would sign in with the password alone.
        if (await signIn.UserManager.GetTwoFactorEnabledAsync(user))
        {
            return SignInResult.TwoFactorRequired;
        }

        var tenantId = await SignInTenant.ChooseAsync(dataSource, user.Id, cancellationToken);
        List<Claim> claims = [new(Claims.AuthenticationMethodReference, "pwd")];
        if (tenantId is not null)
        {
            claims.Add(new Claim(TenantClaims.TenantId, tenantId.Value.ToString()));
        }

        await signIn.SignInWithClaimsAsync(user, isPersistent: true, claims);
        return SignInResult.Success;
    }
}
