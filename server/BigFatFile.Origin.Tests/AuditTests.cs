using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using BigFatFile.Contracts.Api;
using BigFatFile.Origin.Database.Seeds;
using BigFatFile.Origin.Jobs;
using BigFatFile.Origin.Models;
using Dapper;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace BigFatFile.Origin.Tests;

/// <summary>The audit log: what sign-ins write, the read endpoint and the monthly partitions. Rows are read as the owner.</summary>
public sealed class AuditTests(Origin origin) : IClassFixture<Origin>
{
    private const string UserAgent = "BigFatFileTests/1.0";

    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    [Fact]
    public async Task APasswordSignInIsRecordedInTheChosenTenant()
    {
        var userId = await TestData.CreateUserAsync(origin, "audit-signed-in@bigfatfile.local");
        var tenantId = await TestData.CreateTenantAsync(origin, "Audit signed in", userId);
        using var browser = Browser();

        await SignIn.SignInWithPasswordAsync(browser, "audit-signed-in@bigfatfile.local", TestData.Password);

        var row = Assert.Single(await RowsOfAsync(userId));
        Assert.Equal(((Guid?)tenantId, AuditModel.SignedIn, (string?)null), (row.TenantId, row.Action, row.AgentClientId));
        Assert.Equal((Origin.ClientAddress.ToString(), UserAgent), (row.IpAddress, row.UserAgent));
    }

    [Fact]
    public async Task AnAuthorizationCodeIsRecordedWithTheClientAsAgent()
    {
        var userId = await TestData.CreateUserAsync(origin, "audit-cli@bigfatfile.local");
        var tenantId = await TestData.CreateTenantAsync(origin, "Audit CLI", userId);
        using var browser = Browser();
        await SignIn.SignInWithPasswordAsync(browser, "audit-cli@bigfatfile.local", TestData.Password);

        var tokens = await SignIn.AuthorizationCodeAsync(browser, "openid offline_access");
        var refreshed = await browser.PostAsync("/connect/token", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "refresh_token",
            ["client_id"] = DevelopmentSeed.CliClientId,
            ["refresh_token"] = tokens.GetProperty("refresh_token").GetString()!,
        }), Cancellation);
        Assert.Equal(HttpStatusCode.OK, refreshed.StatusCode);

        var rows = await RowsOfAsync(userId);
        Assert.Equal([AuditModel.SignedIn, AuditModel.AppAuthorized], rows.Select(row => row.Action));
        Assert.Equal(((Guid?)tenantId, (string?)DevelopmentSeed.CliClientId), (rows[1].TenantId, rows[1].AgentClientId));
    }

    [Fact]
    public async Task AWrongPasswordIsRecordedOutsideAnyTenant()
    {
        var userId = await TestData.CreateUserAsync(origin, "audit-wrong@bigfatfile.local");
        await TestData.CreateTenantAsync(origin, "Audit wrong", userId);
        using var browser = Browser();

        await SignIn.PostSignInFormAsync(browser, "audit-wrong@bigfatfile.local", "not the password");

        var row = Assert.Single(await RowsOfAsync(userId));
        Assert.Equal(((Guid?)null, AuditModel.SignInFailed, (string?)"wrong_password"), (row.TenantId, row.Action, row.Reason));
        Assert.Equal((Origin.ClientAddress.ToString(), UserAgent), (row.IpAddress, row.UserAgent));
    }

    [Fact]
    public async Task AnUnknownAddressIsNotRecorded()
    {
        var before = await CountAsync();
        using var browser = Browser();

        await SignIn.PostSignInFormAsync(browser, "nobody@bigfatfile.local", "any password");

        Assert.Equal(before, await CountAsync());
    }

    [Fact]
    public async Task TheListIsNewestFirstAndFollowsTheCursor()
    {
        using var browser = Browser();
        var (tenantId, adminId) = await AdminOfNewTenantAsync(browser, "audit-pages");
        var start = Later();
        var times = Enumerable.Range(0, 5).Select(minute => start.AddMinutes(minute)).ToList();
        foreach (var time in times)
        {
            await InsertAsync(tenantId, adminId, time);
        }

        List<Page<AuditEvent>> pages = [];
        string? cursor = null;
        do
        {
            var page = await ListAsync(browser, tenantId, $"from={Format(start)}&limit=2" + (cursor is null ? "" : $"&cursor={cursor}"));
            pages.Add(page);
            cursor = page.NextCursor;
        }
        while (cursor is not null);

        Assert.Equal([2, 2, 1], pages.Select(page => page.Items.Count));
        Assert.Equal(Enumerable.Reverse(times), pages.SelectMany(page => page.Items).Select(item => item.OccurredAt));
        var first = pages[0].Items[0];
        Assert.Equal((AuditAction.SignedIn, adminId, "audit-pages@bigfatfile.local"), (first.Action, first.Actor!.UserId, first.Actor.Email));
    }

    [Fact]
    public async Task TheListFiltersByActorAndTime()
    {
        using var browser = Browser();
        var (tenantId, adminId) = await AdminOfNewTenantAsync(browser, "audit-filters");
        var otherId = await TestData.CreateUserAsync(origin, "audit-filters-other@bigfatfile.local");
        var start = Later();
        await InsertAsync(tenantId, adminId, start);
        await InsertAsync(tenantId, otherId, start.AddMinutes(1));
        await InsertAsync(tenantId, otherId, start.AddMinutes(2));

        var byOther = await ListAsync(browser, tenantId, $"from={Format(start)}&actor={otherId}");
        var inRange = await ListAsync(browser, tenantId, $"from={Format(start.AddMinutes(1))}&to={Format(start.AddMinutes(2))}");

        Assert.Equal([start.AddMinutes(2), start.AddMinutes(1)], byOther.Items.Select(item => item.OccurredAt));
        Assert.Equal([start.AddMinutes(1)], inRange.Items.Select(item => item.OccurredAt));
    }

    [Fact]
    public async Task OnlyAnAdminReadsTheLog()
    {
        var userId = await TestData.CreateUserAsync(origin, "audit-member@bigfatfile.local");
        var tenantId = await TestData.CreateTenantAsync(origin, "Audit member", userId);
        using var browser = Browser();
        await SignIn.SignInWithPasswordAsync(browser, "audit-member@bigfatfile.local", TestData.Password);

        var response = await browser.GetAsync($"/api/v1/tenants/{tenantId}/audit", Cancellation);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal(ErrorCodes.AdminRequired, await CodeAsync(response));
    }

    [Theory]
    [InlineData("cursor=not-a-cursor")]
    [InlineData("limit=0")]
    [InlineData("limit=501")]
    public async Task ABadCursorOrLimitIsRefused(string query)
    {
        using var browser = Browser();
        var (tenantId, _) = await AdminOfNewTenantAsync(browser, $"audit-bad-{Guid.CreateVersion7():N}");

        var response = await browser.GetAsync($"/api/v1/tenants/{tenantId}/audit?{query}", Cancellation);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(ErrorCodes.BadRequest, await CodeAsync(response));
    }

    [Fact]
    public async Task StartupCreatesThePartitionsOfThisMonthAndTheNextTwo()
    {
        var month = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1, 0, 0, 0, DateTimeKind.Utc);

        var partitions = await PartitionsAsync();

        foreach (var ahead in Enumerable.Range(0, AuditPartitionsJob.MonthsAhead + 1))
        {
            Assert.Contains(PartitionName(month.AddMonths(ahead)), partitions);
        }
    }

    [Fact]
    public async Task TheJobDropsThePartitionsPastRetention()
    {
        await using (var connection = await OpenAsOwnerAsync())
        {
            await connection.ExecuteAsync(
                "CREATE TABLE audit_events_2020_01 PARTITION OF audit_events FOR VALUES FROM ('2020-01-01T00:00:00Z') TO ('2020-02-01T00:00:00Z')");
        }

        await origin.Factory.Services.GetRequiredService<AuditPartitionsJob>().RunAsync(Cancellation);

        var partitions = await PartitionsAsync();
        Assert.DoesNotContain("audit_events_2020_01", partitions);
        Assert.Contains(PartitionName(DateTime.UtcNow), partitions);
    }

    private HttpClient Browser()
    {
        var browser = origin.CreateClient();
        browser.DefaultRequestHeaders.UserAgent.ParseAdd(UserAgent);
        return browser;
    }

    /// <summary>Signs the browser in as the admin of a new tenant.</summary>
    private async Task<(Guid TenantId, Guid AdminId)> AdminOfNewTenantAsync(HttpClient browser, string name)
    {
        var email = name + "@bigfatfile.local";
        var adminId = await TestData.CreateUserAsync(origin, email);
        var tenantId = await TestData.CreateTenantAsync(origin, name);
        await TestData.AddMemberAsync(origin, tenantId, adminId, TenantRole.Admin);
        await SignIn.SignInWithPasswordAsync(browser, email, TestData.Password);
        return (tenantId, adminId);
    }

    // After the sign-in's own row, and in whole seconds, which the database stores exactly.
    private static DateTimeOffset Later() =>
        DateTimeOffset.FromUnixTimeSeconds(DateTimeOffset.UtcNow.AddHours(1).ToUnixTimeSeconds());

    private static string Format(DateTimeOffset time) =>
        Uri.EscapeDataString(time.UtcDateTime.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture));

    private static string PartitionName(DateTime month) =>
        string.Create(CultureInfo.InvariantCulture, $"audit_events_{month:yyyy_MM}");

    private static async Task<Page<AuditEvent>> ListAsync(HttpClient browser, Guid tenantId, string query) =>
        (await browser.GetFromJsonAsync<Page<AuditEvent>>($"/api/v1/tenants/{tenantId}/audit?{query}", Cancellation))!;

    private static async Task<string?> CodeAsync(HttpResponseMessage response)
    {
        using var problem = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Cancellation));
        return problem.RootElement.GetProperty("code").GetString();
    }

    private async Task<NpgsqlConnection> OpenAsOwnerAsync()
    {
        var connection = new NpgsqlConnection(origin.ConnectionString);
        await connection.OpenAsync(Cancellation);
        return connection;
    }

    private async Task InsertAsync(Guid tenantId, Guid userId, DateTimeOffset time)
    {
        await using var connection = await OpenAsOwnerAsync();
        await connection.ExecuteAsync(
            "INSERT INTO audit_events (occurred_at, tenant_id, action, actor_user_id) VALUES (@Time, @TenantId, @Action, @UserId)",
            new { Time = time, TenantId = tenantId, Action = AuditModel.SignedIn, UserId = userId });
    }

    private async Task<long> CountAsync()
    {
        await using var connection = await OpenAsOwnerAsync();
        return await connection.ExecuteScalarAsync<long>("SELECT count(*) FROM audit_events");
    }

    private async Task<IReadOnlySet<string>> PartitionsAsync()
    {
        await using var connection = await OpenAsOwnerAsync();
        var names = await connection.QueryAsync<string>(
            "SELECT c.relname FROM pg_inherits i JOIN pg_class c ON c.oid = i.inhrelid WHERE i.inhparent = 'audit_events'::regclass");
        return names.ToHashSet();
    }

    private async Task<IReadOnlyList<Row>> RowsOfAsync(Guid userId)
    {
        await using var connection = await OpenAsOwnerAsync();
        var rows = await connection.QueryAsync<Row>(
            """
            SELECT tenant_id AS TenantId, action AS Action, agent_client_id AS AgentClientId,
                   host(ip_address) AS IpAddress, user_agent AS UserAgent, details ->> 'reason' AS Reason
            FROM audit_events WHERE actor_user_id = @UserId ORDER BY occurred_at, id
            """,
            new { UserId = userId });
        return [.. rows];
    }

    private sealed record Row(Guid? TenantId, string Action, string? AgentClientId, string? IpAddress, string? UserAgent, string? Reason);
}
