using System.Globalization;
using BigFatFile.Origin.Database;
using Dapper;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace BigFatFile.Origin.Tests;

/// <summary>The policies, checked directly as the user that requests connect as.</summary>
public sealed class RowLevelSecurityTests(Origin origin) : IClassFixture<Origin>
{
    [Fact]
    public async Task WithoutATenantOrAUserNothingIsVisible()
    {
        await using var connection = await OpenAsAppAsync();

        Assert.Equal(0, await connection.ExecuteScalarAsync<long>("SELECT count(*) FROM tenant_members"));
        Assert.Equal(0, await connection.ExecuteScalarAsync<long>("SELECT count(*) FROM tenants"));

        // After a transaction that set them, the settings read back as empty, not as unset.
        await using (var transaction = await connection.BeginTransactionAsync(TestContext.Current.CancellationToken))
        {
            await connection.ExecuteAsync("SELECT set_config('app.tenant_id', @Id, true)", new { Id = Guid.NewGuid().ToString() }, transaction);
            await transaction.CommitAsync(TestContext.Current.CancellationToken);
        }

        Assert.Equal(0, await connection.ExecuteScalarAsync<long>("SELECT count(*) FROM tenant_members"));
    }

    [Fact]
    public async Task ATenantSeesOnlyItsOwnMemberships()
    {
        var (alice, bob) = (await TestData.CreateUserAsync(origin, "alice@bigfatfile.local"), await TestData.CreateUserAsync(origin, "bob@bigfatfile.local"));
        var aliceTenant = await TestData.CreateTenantAsync(origin, "Alice's", alice);
        await TestData.CreateTenantAsync(origin, "Bob's", bob);
        await using var connection = await OpenAsAppAsync();
        await using var transaction = await BeginAsync(connection, tenantId: aliceTenant, userId: null);

        var tenants = await connection.QueryAsync<Guid>("SELECT tenant_id FROM tenant_members", transaction: transaction);

        Assert.Equal([aliceTenant], tenants);
    }

    [Fact]
    public async Task AUserSeesTheirMembershipsInEveryTenant()
    {
        var carol = await TestData.CreateUserAsync(origin, "carol@bigfatfile.local");
        var first = await TestData.CreateTenantAsync(origin, "Carol's first", carol);
        var second = await TestData.CreateTenantAsync(origin, "Carol's second", carol);
        await using var connection = await OpenAsAppAsync();
        await using var transaction = await BeginAsync(connection, tenantId: null, userId: carol);

        var memberships = await connection.QueryAsync<Guid>("SELECT tenant_id FROM tenant_members ORDER BY tenant_id", transaction: transaction);
        var tenants = await connection.QueryAsync<Guid>("SELECT id FROM tenants ORDER BY id", transaction: transaction);

        Assert.Equal(new[] { first, second }.Order(), memberships);
        Assert.Equal(new[] { first, second }.Order(), tenants);
    }

    [Fact]
    public async Task AUserCannotAddThemselvesToAnotherTenant()
    {
        var dave = await TestData.CreateUserAsync(origin, "dave@bigfatfile.local");
        var daveTenant = await TestData.CreateTenantAsync(origin, "Dave's", dave);
        var otherTenant = await TestData.CreateTenantAsync(origin, "Not Dave's");
        await using var connection = await OpenAsAppAsync();
        await using var transaction = await BeginAsync(connection, tenantId: daveTenant, userId: dave);

        var error = await Assert.ThrowsAsync<PostgresException>(() => connection.ExecuteAsync(
            "INSERT INTO tenant_members (tenant_id, user_id, role) VALUES (@TenantId, @UserId, 'admin')",
            new { TenantId = otherTenant, UserId = dave },
            transaction));

        Assert.Equal(PostgresErrorCodes.InsufficientPrivilege, error.SqlState);
    }

    [Fact]
    public async Task ATenantSeesOnlyItsOwnAuditEvents()
    {
        var mine = await TestData.CreateTenantAsync(origin, "Audit mine");
        var other = await TestData.CreateTenantAsync(origin, "Audit other");
        await InsertAuditEventAsOwnerAsync(mine);
        await InsertAuditEventAsOwnerAsync(other);
        await InsertAuditEventAsOwnerAsync(tenantId: null);
        await using var connection = await OpenAsAppAsync();
        await using var transaction = await BeginAsync(connection, tenantId: mine, userId: null);

        var tenants = await connection.QueryAsync<Guid?>("SELECT tenant_id FROM audit_events", transaction: transaction);

        Assert.Equal([mine], tenants);
    }

    [Theory]
    [InlineData("UPDATE audit_events SET action = 'changed'")]
    [InlineData("DELETE FROM audit_events")]
    public async Task AuditEventsCannotBeChangedOrDeleted(string sql)
    {
        var tenantId = await TestData.CreateTenantAsync(origin, "Audit append-only");
        await InsertAuditEventAsOwnerAsync(tenantId);
        await using var connection = await OpenAsAppAsync();
        await using var transaction = await BeginAsync(connection, tenantId, userId: null);

        var error = await Assert.ThrowsAsync<PostgresException>(() => connection.ExecuteAsync(sql, transaction: transaction));

        Assert.Equal(PostgresErrorCodes.InsufficientPrivilege, error.SqlState);
    }

    [Fact]
    public async Task ARequestCannotWriteAnAuditEventIntoAnotherTenant()
    {
        var mine = await TestData.CreateTenantAsync(origin, "Audit writer");
        var other = await TestData.CreateTenantAsync(origin, "Audit target");
        await using var connection = await OpenAsAppAsync();
        await using var transaction = await BeginAsync(connection, tenantId: mine, userId: null);

        var error = await Assert.ThrowsAsync<PostgresException>(() => connection.ExecuteAsync(
            "INSERT INTO audit_events (tenant_id, action) VALUES (@TenantId, 'signed_in')", new { TenantId = other }, transaction));

        Assert.Equal(PostgresErrorCodes.InsufficientPrivilege, error.SqlState);
    }

    [Fact]
    public async Task APartitionCannotBeReadDirectly()
    {
        // A second start grants every table again, partitions included, and must take them back.
        await origin.Factory.Services.GetRequiredService<DatabaseBootstrap>().PrepareAsync(TestContext.Current.CancellationToken);
        var partition = string.Create(CultureInfo.InvariantCulture, $"audit_events_{DateTime.UtcNow:yyyy_MM}");
        await using var connection = await OpenAsAppAsync();

        var error = await Assert.ThrowsAsync<PostgresException>(() => connection.ExecuteAsync($"SELECT count(*) FROM {partition}"));

        Assert.Equal(PostgresErrorCodes.InsufficientPrivilege, error.SqlState);
    }

    private async Task InsertAuditEventAsOwnerAsync(Guid? tenantId)
    {
        await using var connection = new NpgsqlConnection(origin.ConnectionString);
        await connection.ExecuteAsync(
            "INSERT INTO audit_events (tenant_id, action) VALUES (@TenantId, 'signed_in')", new { TenantId = tenantId });
    }

    private async Task<NpgsqlConnection> OpenAsAppAsync()
    {
        var connection = new NpgsqlConnection(origin.AppConnectionString);
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        return connection;
    }

    private static Task<NpgsqlTransaction> BeginAsync(NpgsqlConnection connection, Guid? tenantId, Guid? userId) =>
        ScopedDatabase.BeginTenantTransactionAsync(connection, tenantId, userId, TestContext.Current.CancellationToken);
}
