using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using BigFatFile.Contracts.Api;
using BigFatFile.Origin.Database.Seeds;
using Dapper;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Routing.Patterns;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace BigFatFile.Origin.Tests;

public sealed class TenantIsolationTests(Origin origin, Postgres postgres) : IClassFixture<Origin>
{
    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    [Fact]
    public async Task EveryTenantEndpointAnswers404ForAnotherTenant()
    {
        var otherTenant = await TestData.CreateTenantAsync(origin, "Other");
        using var browser = origin.CreateClient();
        await SignIn.SignInWithPasswordAsync(browser, DevelopmentSeed.UserEmail, DevelopmentSeed.UserPassword);
        var ownTenant = (await browser.GetFromJsonAsync<Me>("/api/v1/account/me", Cancellation))!.TenantId!.Value;

        var endpoints = origin.Factory.Services.GetRequiredService<EndpointDataSource>().Endpoints
            .OfType<RouteEndpoint>()
            .Where(endpoint => endpoint.RoutePattern.RawText!.StartsWith("/api/v1/tenants/{id", StringComparison.Ordinal))
            .ToList();
        Assert.NotEmpty(endpoints);

        foreach (var endpoint in endpoints)
        {
            var name = endpoint.RoutePattern.RawText;

            // The same request for the session's own tenant shows that the route matches.
            var own = await SendAsync(browser, endpoint, ownTenant);
            Assert.True(own.StatusCode != HttpStatusCode.NotFound, $"{name} did not match its own tenant.");

            var other = await SendAsync(browser, endpoint, otherTenant);
            Assert.True(other.StatusCode == HttpStatusCode.NotFound, $"{name} answered {(int)other.StatusCode} for another tenant.");
            using var problem = JsonDocument.Parse(await other.Content.ReadAsStringAsync(Cancellation));
            Assert.Equal(ErrorCodes.NotFound, problem.RootElement.GetProperty("code").GetString());
        }
    }

    private static async Task<HttpResponseMessage> SendAsync(HttpClient browser, RouteEndpoint endpoint, Guid tenantId)
    {
        var method = endpoint.Metadata.GetMetadata<HttpMethodMetadata>()!.HttpMethods[0];
        using var request = new HttpRequestMessage(new HttpMethod(method), PathFor(endpoint.RoutePattern, tenantId));
        if (method is "POST" or "PUT" or "PATCH")
        {
            request.Content = JsonContent.Create(new { });
        }

        return await browser.SendAsync(request, Cancellation);
    }

    [Fact]
    public async Task TheSessionsOwnTenantIsServed()
    {
        using var browser = origin.CreateClient();
        await SignIn.SignInWithPasswordAsync(browser, DevelopmentSeed.UserEmail, DevelopmentSeed.UserPassword);
        var me = await browser.GetFromJsonAsync<Me>("/api/v1/account/me", Cancellation);

        var tenant = await browser.GetFromJsonAsync<Tenant>($"/api/v1/tenants/{me!.TenantId}", Cancellation);

        Assert.Equal(DevelopmentSeed.TenantName, tenant!.Name);
    }

    [Fact]
    public async Task AMemberWhoIsRemovedLosesAccessAtOnce()
    {
        var userId = await TestData.CreateUserAsync(origin, "removed@bigfatfile.local");
        var tenantId = await TestData.CreateTenantAsync(origin, "Removed", userId);
        using var browser = origin.CreateClient();
        await SignIn.SignInWithPasswordAsync(browser, "removed@bigfatfile.local", TestData.Password);
        Assert.Equal(HttpStatusCode.OK, (await browser.GetAsync($"/api/v1/tenants/{tenantId}", Cancellation)).StatusCode);

        await using (var connection = new NpgsqlConnection(origin.ConnectionString))
        {
            await connection.ExecuteAsync("DELETE FROM tenant_members WHERE tenant_id = @TenantId", new { TenantId = tenantId });
        }

        Assert.Equal(HttpStatusCode.NotFound, (await browser.GetAsync($"/api/v1/tenants/{tenantId}", Cancellation)).StatusCode);
    }

    [Fact]
    public async Task SignInChoosesTheTenantUsedLast()
    {
        var userId = await TestData.CreateUserAsync(origin, "two-tenants@bigfatfile.local");
        var first = await TestData.CreateTenantAsync(origin, "First", userId);
        var second = await TestData.CreateTenantAsync(origin, "Second", userId);

        Assert.Equal(first, await SessionTenantAsync("two-tenants@bigfatfile.local"));

        await using (var connection = new NpgsqlConnection(origin.ConnectionString))
        {
            await connection.ExecuteAsync(
                "UPDATE tenant_members SET last_signed_in_at = now() + interval '1 hour' WHERE tenant_id = @TenantId",
                new { TenantId = second });
        }

        Assert.Equal(second, await SessionTenantAsync("two-tenants@bigfatfile.local"));
    }

    [Fact]
    public async Task TheTenantSurvivesTheSecurityStampCheck()
    {
        await using var factory = origin.Factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
            services.Configure<SecurityStampValidatorOptions>(options => options.ValidationInterval = TimeSpan.Zero)));
        using var browser = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = true });
        await SignIn.SignInWithPasswordAsync(browser, DevelopmentSeed.UserEmail, DevelopmentSeed.UserPassword);

        var refreshed = await browser.GetAsync("/api/v1/account/me", Cancellation);
        var me = await browser.GetFromJsonAsync<Me>("/api/v1/account/me", Cancellation);

        Assert.True(refreshed.Headers.Contains("Set-Cookie"), "The security stamp check did not rebuild the session.");
        Assert.NotNull(me!.TenantId);
    }

    [Fact]
    public async Task StartupRefusesAUserThatSkipsRowLevelSecurity()
    {
        var owner = await postgres.CreateDatabaseAsync();
        await using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder => builder
            .UseSetting("BFF_DATABASE", owner)
            .UseSetting("BFF_DATABASE_OWNER", owner)
            .UseSetting("BFF_MODULES_PATH", Path.Combine(AppContext.BaseDirectory, "no-modules")));

        var error = Assert.ThrowsAny<Exception>(() => factory.CreateClient());

        Assert.Contains("does not obey RLS (row-level security)", error.ToString());
    }

    private async Task<Guid?> SessionTenantAsync(string email)
    {
        using var browser = origin.CreateClient();
        await SignIn.SignInWithPasswordAsync(browser, email, TestData.Password);
        return (await browser.GetFromJsonAsync<Me>("/api/v1/account/me", Cancellation))!.TenantId;
    }

    private static string PathFor(RoutePattern pattern, Guid tenantId) =>
        "/" + string.Join("/", pattern.PathSegments.Select(segment => string.Concat(segment.Parts.Select(part => part switch
        {
            RoutePatternParameterPart { Name: "id" } => tenantId.ToString(),
            RoutePatternParameterPart => Guid.NewGuid().ToString(),
            RoutePatternLiteralPart literal => literal.Content,
            _ => "",
        }))));
}
