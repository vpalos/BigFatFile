using System.Security.Claims;
using BigFatFile.Origin.Auth.Users;
using BigFatFile.Origin.Models;
using Microsoft.AspNetCore.Identity;
using Npgsql;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace BigFatFile.Origin.Auth.SignIn;

/// <summary>Signs a user in with a password. Every form and endpoint that takes a password calls this.</summary>
internal static class PasswordSignIn
{
    /// <summary>
    /// Checks the password, then starts a session in the tenant that <see cref="SignInTenant"/> chooses.
    /// A refusal is written to the audit log outside any tenant.
    /// </summary>
    public static async Task<SignInResult> SignInAsync(
        SignInManager<BffUser> signIn, NpgsqlDataSource dataSource, BffUser user, string password, HttpContext context)
    {
        var result = await signIn.CheckPasswordSignInAsync(user, password, lockoutOnFailure: true);

        // The password check ignores two-factor sign-in, and this server has no second step yet.
        // Without this refusal, such an account would sign in with the password alone.
        if (result.Succeeded && await signIn.UserManager.GetTwoFactorEnabledAsync(user))
        {
            result = SignInResult.TwoFactorRequired;
        }

        if (!result.Succeeded)
        {
            var reason = result switch
            {
                { IsLockedOut: true } => "locked_out",
                { IsNotAllowed: true } => "not_allowed",
                { RequiresTwoFactor: true } => "two_factor_required",
                _ => "wrong_password",
            };
            await SignInAudit.WriteAsync(
                dataSource,
                SignInAudit.Entry(context, tenantId: null, AuditModel.SignInFailed, user.Id, details: new Dictionary<string, string> { ["reason"] = reason }),
                context.RequestAborted);
            return result;
        }

        var tenantId = await SignInTenant.ChooseAsync(dataSource, user.Id, context);
        List<Claim> claims = [new(Claims.AuthenticationMethodReference, "pwd")];
        if (tenantId is not null)
        {
            claims.Add(new Claim(TenantClaims.TenantId, tenantId.Value.ToString()));
        }

        await signIn.SignInWithClaimsAsync(user, isPersistent: true, claims);
        return SignInResult.Success;
    }
}
