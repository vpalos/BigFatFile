using BigFatFile.Server.Auth;
using BigFatFile.Server.Jobs;
using Dapper;
using Microsoft.AspNetCore.Identity;
using Npgsql;
using OpenIddict.Abstractions;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace BigFatFile.Server.Development;

/// <summary>
/// With <c>BFF_DEV_SEED=true</c>: a demo tenant, a demo user and two ready-made clients, so a
/// fresh checkout can sign in at once. The values are published in the README, which is why
/// the settings refuse this outside Development.
/// </summary>
internal sealed class DevSeed(
    UserManager<BffUser> users,
    RoleManager<IdentityRole<Guid>> roles,
    IOpenIddictApplicationManager applications,
    NpgsqlDataSource dataSource,
    ILogger<DevSeed> logger)
{
    public const string UserEmail = "demo@bigfatfile.local";
    public const string UserPassword = "bigfatfile-demo";
    public const string TenantName = "Demo";

    /// <summary>A public client for scripts and the CLI: authorization code with PKCE and a loopback redirect.</summary>
    public const string CliClientId = "bff-dev-cli";
    public const string CliRedirectUri = "http://127.0.0.1:53682/callback";

    /// <summary>A confidential client for an App acting as itself: client credentials.</summary>
    public const string AppClientId = "bff-dev-app";
    public const string AppClientSecret = "bff-dev-app-secret";

    public async Task RunAsync(CancellationToken cancellationToken)
    {
        var user = await EnsureUserAsync();
        await EnsureTenantAsync(user, cancellationToken);
        await EnsureClientsAsync(cancellationToken);
        logger.LogWarning("Development seed is on: {Email} signs in with the published demo password", UserEmail);
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
        await connection.ExecuteAsync(new CommandDefinition(
            """
            WITH existing AS (
                SELECT t.id FROM tenants t
                JOIN tenant_members m ON m.tenant_id = t.id
                WHERE t.name = @TenantName AND m.user_id = @UserId
            ),
            created AS (
                INSERT INTO tenants (id, name)
                SELECT @TenantId, @TenantName WHERE NOT EXISTS (SELECT 1 FROM existing)
                RETURNING id
            )
            INSERT INTO tenant_members (tenant_id, user_id, role)
            SELECT id, @UserId, 'admin' FROM created
            """,
            new { TenantId = Guid.CreateVersion7(), TenantName, UserId = user.Id },
            cancellationToken: cancellationToken));
    }

    private async Task EnsureClientsAsync(CancellationToken cancellationToken)
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

        if (await applications.FindByClientIdAsync(AppClientId, cancellationToken) is null)
        {
            var app = new OpenIddictApplicationDescriptor
            {
                ClientId = AppClientId,
                ClientSecret = AppClientSecret,
                DisplayName = "Development App",
                ClientType = ClientTypes.Confidential,
                Permissions = { Permissions.Endpoints.Token, Permissions.GrantTypes.ClientCredentials },
            };
            foreach (var scope in BffScopes.Api)
            {
                app.Permissions.Add(BffScopes.Permission(scope));
            }

            await applications.CreateAsync(app, cancellationToken);
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
