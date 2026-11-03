using Microsoft.AspNetCore.Authorization;
using OpenIddict.Abstractions;

namespace BigFatFile.Origin.Auth.Tokens;

internal static class ScopeAuthorization
{
    public static string PolicyFor(string scope) => "scope:" + scope;

    public static AuthorizationBuilder AddScopePolicies(this AuthorizationBuilder builder)
    {
        foreach (var scope in BffScopes.All)
        {
            builder.AddPolicy(PolicyFor(scope), policy => policy.AddRequirements(new ScopeRequirement(scope)));
        }

        return builder;
    }

    public static string? ScopeOf(string? policy) =>
        policy is not null && policy.StartsWith("scope:", StringComparison.Ordinal) ? policy["scope:".Length..] : null;
}

internal sealed record ScopeRequirement(string Scope) : IAuthorizationRequirement;

internal sealed class ScopeRequirementHandler : AuthorizationHandler<ScopeRequirement>
{
    protected override Task HandleRequirementAsync(AuthorizationHandlerContext context, ScopeRequirement requirement)
    {
        // The web app on the same origin uses the session cookie, which carries no scopes.
        // It may do whatever its user may do, except what only a token may do.
        var bySession = AuthServices.UsesSession(context.User) && !BffScopes.TokenOnly.Contains(requirement.Scope);
        if (bySession || (context.User.Identity?.IsAuthenticated == true && context.User.HasScope(requirement.Scope)))
        {
            context.Succeed(requirement);
        }

        return Task.CompletedTask;
    }
}
