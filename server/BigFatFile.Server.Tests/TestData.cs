using BigFatFile.Contracts.Api;
using BigFatFile.Server.Auth;
using BigFatFile.Server.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace BigFatFile.Server.Tests;

/// <summary>Users and tenants for tests. Tenants are written as the owner, so row-level security does not apply.</summary>
public static class TestData
{
    public const string Password = "a-test-password";

    public static async Task<Guid> CreateUserAsync(Origin origin, string email)
    {
        using var scope = origin.Factory.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<BffUser>>();
        var user = new BffUser { UserName = email, Email = email, EmailConfirmed = true };
        var created = await users.CreateAsync(user, Password);
        Assert.True(created.Succeeded, string.Join(" ", created.Errors.Select(error => error.Description)));
        return user.Id;
    }

    public static async Task<Guid> CreateTenantAsync(Origin origin, string name, params Guid[] members)
    {
        var tenantId = Guid.CreateVersion7();
        var cancellation = TestContext.Current.CancellationToken;
        await using var connection = new NpgsqlConnection(origin.ConnectionString);
        await connection.OpenAsync(cancellation);
        await TenantModel.CreateAsync(connection, tenantId, name, cancellation);
        foreach (var userId in members)
        {
            await MemberModel.AddAsync(connection, tenantId, userId, TenantRole.Member, cancellation);
        }

        return tenantId;
    }
}
