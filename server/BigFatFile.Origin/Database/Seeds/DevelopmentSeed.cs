using BigFatFile.Contracts.Api;
using BigFatFile.Origin.Auth.Tokens;
using BigFatFile.Origin.Auth.Users;
using BigFatFile.Origin.Jobs;
using BigFatFile.Origin.Models;
using Microsoft.AspNetCore.Identity;
using Npgsql;
using OpenIddict.Abstractions;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace BigFatFile.Origin.Database.Seeds;

/// <summary>
/// This data is seeded when <c>BFF_DEVELOPMENT_SEED=true</c> (only during development):
/// - a demo tenant;
/// - a demo user;
/// - a ready-made client for the CLI.
/// </summary>
internal sealed class DevelopmentSeed(
    UserManager<BffUser> users,
    RoleManager<IdentityRole<Guid>> roles,
    IOpenIddictApplicationManager applications,
    // Creating a tenant is outside any one tenant, so it runs as the owner.
    [FromKeyedServices(DatabaseServices.Owner)] NpgsqlDataSource dataSource,
    ILogger<DevelopmentSeed> logger)
{
    public const string UserEmail = "demo@bigfatfile.local";
    public const string UserPassword = "bigfatfile-demo";
    public const string TenantName = "Demo";

    /// <summary>
    /// A public client for scripts and the CLI:
    /// - authorization code with PKCE;
    /// - a loopback redirect.
    /// </summary>
    public const string CliClientId = "bff-dev-cli";
    public const string CliRedirectUri = "http://127.0.0.1:53682/callback";

    public async Task RunAsync(CancellationToken cancellationToken)
    {
        var user = await EnsureUserAsync();
        await EnsureTenantAsync(user, cancellationToken);
        await EnsureCliClientAsync(cancellationToken);
        logger.LogWarning("Development seeding is ON ({Email}).", UserEmail);
    }

    private async Task<BffUser> EnsureUserAsync()
    {
        if (!await roles.RoleExistsAsync(JobServices.OperatorRole))
        {
            Check(await roles.CreateAsync(new IdentityRole<Guid>(JobServices.OperatorRole) { Id = Guid.CreateVersion7() }));
        }

        var user = await users.FindByEmailAsync(UserEmail);
        if (user is null)
        {
            user = new BffUser { UserName = UserEmail, Email = UserEmail, EmailConfirmed = true };
            Check(await users.CreateAsync(user, UserPassword));
            Check(await users.AddToRoleAsync(user, JobServices.OperatorRole));
        }

        return user;
    }

    private async Task EnsureTenantAsync(BffUser user, CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        var tenants = await MemberModel.ListTenantsOfUserAsync(connection, user.Id, cancellationToken);
        if (tenants.Any(tenant => tenant.TenantName == TenantName))
        {
            return;
        }

        var tenantId = Guid.CreateVersion7();
        await TenantModel.CreateAsync(connection, tenantId, TenantName, cancellationToken);
        await MemberModel.AddAsync(connection, tenantId, user.Id, TenantRole.Admin, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    private async Task EnsureCliClientAsync(CancellationToken cancellationToken)
    {
        if (await applications.FindByClientIdAsync(CliClientId, cancellationToken) is null)
        {
            var cli = new OpenIddictApplicationDescriptor
            {
                ClientId = CliClientId,
                DisplayName = "Development CLI",
                ClientType = ClientTypes.Public,
                ConsentType = ConsentTypes.Implicit,
                RedirectUris = { new Uri(CliRedirectUri) },
                Permissions =
                {
                    Permissions.Endpoints.Authorization,
                    Permissions.Endpoints.Token,
                    Permissions.GrantTypes.AuthorizationCode,
                    Permissions.GrantTypes.RefreshToken,
                    Permissions.ResponseTypes.Code,
                },
                Requirements = { Requirements.Features.ProofKeyForCodeExchange },
            };
            foreach (var scope in BffScopes.All.Where(s => s != BffScopes.OpenId))
            {
                cli.Permissions.Add(BffScopes.Permission(scope));
            }

            await applications.CreateAsync(cli, cancellationToken);
        }
    }

    private static void Check(IdentityResult result)
    {
        if (!result.Succeeded)
        {
            throw new InvalidOperationException(
                "The development seed failed: " + string.Join("; ", result.Errors.Select(e => e.Description)));
        }
    }
}
