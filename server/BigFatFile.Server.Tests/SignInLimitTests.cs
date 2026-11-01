using System.Net;
using System.Text.Json;
using BigFatFile.Contracts.Api;
using BigFatFile.Server.Auth;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;

namespace BigFatFile.Server.Tests;

public sealed class SignInLimitTests(Origin origin) : IClassFixture<Origin>
{
    [Fact]
    public async Task WrongPasswordsInARowLockTheAccount()
    {
        const string email = "lockout@bigfatfile.local";
        const string password = "the-right-password";
        using (var scope = origin.Factory.Services.CreateScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<BffUser>>();
            var created = await users.CreateAsync(new BffUser { UserName = email, Email = email, EmailConfirmed = true }, password);
            Assert.True(created.Succeeded);
        }

        using var browser = origin.CreateClient();
        for (var attempt = 1; attempt < AuthServices.LockoutAttempts; attempt++)
        {
            await SignIn.PostSignInFormAsync(browser, email, "a wrong password");
        }
        Assert.Null(await LockoutEndAsync(email));

        var before = DateTimeOffset.UtcNow;
        await SignIn.PostSignInFormAsync(browser, email, "a wrong password");
        var lockoutEnd = await LockoutEndAsync(email);
        Assert.NotNull(lockoutEnd);
        Assert.InRange(lockoutEnd.Value - before, TimeSpan.FromMinutes(AuthServices.LockoutMinutes) - TimeSpan.FromSeconds(10), TimeSpan.FromMinutes(AuthServices.LockoutMinutes) + TimeSpan.FromSeconds(10));

        var refused = await SignIn.PostSignInFormAsync(browser, email, password);
        Assert.Equal(HttpStatusCode.OK, refused.StatusCode);
        Assert.Contains("Too many failed attempts.", await refused.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task OneAddressGetsTooManyRequestsAfterTheLimit()
    {
        for (var attempt = 0; attempt < SignInRateLimit.AttemptsPerMinute; attempt++)
        {
            Assert.NotEqual(StatusCodes.Status429TooManyRequests, (await PostSignInFromAsync("203.0.113.1")).Response.StatusCode);
        }

        var rejected = await PostSignInFromAsync("203.0.113.1");
        Assert.Equal(StatusCodes.Status429TooManyRequests, rejected.Response.StatusCode);
        Assert.True(int.Parse(rejected.Response.Headers.RetryAfter.ToString()) > 0);
        using var problem = await JsonDocument.ParseAsync(rejected.Response.Body, cancellationToken: TestContext.Current.CancellationToken);
        Assert.Equal(ErrorCodes.TooManyRequests, problem.RootElement.GetProperty("code").GetString());

        Assert.NotEqual(StatusCodes.Status429TooManyRequests, (await PostSignInFromAsync("203.0.113.2")).Response.StatusCode);
    }

    [Theory]
    [InlineData("2001:db8:1::1", "2001:db8:1::2", true)]
    [InlineData("2001:db8:2::1", "2001:db8:2:1::1", false)]
    [InlineData("::ffff:198.51.100.1", "::ffff:198.51.100.2", false)]
    public async Task AddressesShareTheLimitOnlyWithinOneIpv6Block(string first, string second, bool shared)
    {
        for (var attempt = 0; attempt < SignInRateLimit.AttemptsPerMinute; attempt++)
        {
            await PostSignInFromAsync(first);
        }

        var next = await PostSignInFromAsync(second);
        Assert.Equal(shared, next.Response.StatusCode == StatusCodes.Status429TooManyRequests);
    }

    private async Task<DateTimeOffset?> LockoutEndAsync(string email)
    {
        using var scope = origin.Factory.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<BffUser>>();
        return await users.GetLockoutEndDateAsync((await users.FindByEmailAsync(email))!);
    }

    // The test client cannot choose its address, so these requests go to the server directly.
    // They carry no form token: the server refuses them, but the limit counts them first.
    private Task<HttpContext> PostSignInFromAsync(string address) =>
        origin.Factory.Server.SendAsync(context =>
        {
            context.Connection.RemoteIpAddress = IPAddress.Parse(address);
            context.Request.Method = HttpMethods.Post;
            context.Request.Path = "/account/sign-in";
            context.Request.ContentType = "application/x-www-form-urlencoded";
            context.Request.Body = new MemoryStream("email=someone%40example.com&password=x"u8.ToArray());
        }, TestContext.Current.CancellationToken);
}
