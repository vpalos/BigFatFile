using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using BigFatFile.Contracts.Api;
using BigFatFile.Server.Auth;
using BigFatFile.Server.Development;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.DependencyInjection;

namespace BigFatFile.Server.Tests;

public sealed class AuthTests(Origin origin) : IClassFixture<Origin>
{
    [Fact]
    public async Task MeNeedsAToken()
    {
        using var client = origin.CreateClient();

        var response = await client.GetAsync("/api/v1/account/me", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task AnAuthorizationWithoutASessionGoesToTheSignInForm()
    {
        using var browser = origin.CreateClient();

        var response = await browser.GetAsync(QueryHelpers.AddQueryString("/connect/authorize", new Dictionary<string, string?>
        {
            ["client_id"] = DevSeed.CliClientId,
            ["redirect_uri"] = DevSeed.CliRedirectUri,
            ["response_type"] = "code",
            ["scope"] = "openid",
            ["code_challenge"] = "E9Melhoa2OwvFrEMTJguCHaoeK1t8URWbuGJSstw-cM",
            ["code_challenge_method"] = "S256",
        }), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.StartsWith("/account/sign-in", response.Headers.Location!.IsAbsoluteUri ? response.Headers.Location.PathAndQuery : response.Headers.Location.OriginalString);
    }

    [Fact]
    public async Task ACliSignsInWithPkceAndCallsMe()
    {
        using var browser = origin.CreateClient();
        await SignIn.SignInWithPasswordAsync(browser, DevSeed.UserEmail, DevSeed.UserPassword);
        var tokens = await SignIn.AuthorizationCodeAsync(browser, "openid profile email offline_access files.read");

        var accessToken = tokens.GetProperty("access_token").GetString()!;
        Assert.True(tokens.TryGetProperty("refresh_token", out _));
        Assert.Equal("ES256", JwtHeader(accessToken).GetProperty("alg").GetString());

        using var api = origin.CreateClient();
        api.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        var me = await api.GetFromJsonAsync<Me>("/api/v1/account/me", TestContext.Current.CancellationToken);

        Assert.Equal(DevSeed.UserEmail, me!.Email);
        var tenant = Assert.Single(me.Tenants);
        Assert.Equal((DevSeed.TenantName, TenantRole.Admin), (tenant.Name, tenant.Role));
        Assert.Equal(tenant.Id, me.TenantId);
    }

    [Fact]
    public async Task ARefreshTokenGivesANewAccessToken()
    {
        using var browser = origin.CreateClient();
        await SignIn.SignInWithPasswordAsync(browser, DevSeed.UserEmail, DevSeed.UserPassword);
        var tokens = await SignIn.AuthorizationCodeAsync(browser, "openid offline_access");

        using var client = origin.CreateClient();
        var refreshed = await client.PostAsync("/connect/token", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "refresh_token",
            ["client_id"] = DevSeed.CliClientId,
            ["refresh_token"] = tokens.GetProperty("refresh_token").GetString()!,
        }), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, refreshed.StatusCode);
        var body = await refreshed.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", body.GetProperty("access_token").GetString());
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/v1/account/me", TestContext.Current.CancellationToken)).StatusCode);
    }

    [Fact]
    public async Task SigningInNeedsTheFormToken()
    {
        using var browser = origin.CreateClient();

        var response = await browser.PostAsync("/account/sign-in", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["email"] = DevSeed.UserEmail,
            ["password"] = DevSeed.UserPassword,
        }), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task SigningOutNeedsTheCsrfToken()
    {
        using var browser = origin.CreateClient();
        await SignIn.SignInWithPasswordAsync(browser, DevSeed.UserEmail, DevSeed.UserPassword);

        var response = await browser.PostAsync("/api/v1/account/sign-out", null, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await browser.GetAsync("/api/v1/account/me", TestContext.Current.CancellationToken)).StatusCode);
    }

    [Fact]
    public async Task SigningOutEndsTheSession()
    {
        using var browser = origin.CreateClient();
        await SignIn.SignInWithPasswordAsync(browser, DevSeed.UserEmail, DevSeed.UserPassword);

        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/account/sign-out");
        request.Headers.Add("X-CSRF-Token", await SignIn.CsrfTokenAsync(browser));
        var response = await browser.SendAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await browser.GetAsync("/api/v1/account/me", TestContext.Current.CancellationToken)).StatusCode);
    }

    [Fact]
    public async Task TheSessionCookieAlsoReachesTheApi()
    {
        using var browser = origin.CreateClient();
        await SignIn.SignInWithPasswordAsync(browser, DevSeed.UserEmail, DevSeed.UserPassword);

        var me = await browser.GetFromJsonAsync<Me>("/api/v1/account/me", TestContext.Current.CancellationToken);

        Assert.Equal(DevSeed.UserEmail, me!.Email);
    }

    [Fact]
    public async Task AWrongPasswordShowsTheFormAgain()
    {
        using var browser = origin.CreateClient();
        var form = await browser.GetStringAsync("/account/sign-in", TestContext.Current.CancellationToken);
        var token = System.Text.RegularExpressions.Regex.Match(form, "name=\"__RequestVerificationToken\" value=\"([^\"]+)\"").Groups[1].Value;

        var response = await browser.PostAsync("/account/sign-in", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = WebUtility.HtmlDecode(token),
            ["email"] = DevSeed.UserEmail,
            ["password"] = "not the password",
        }), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("The email or the password is wrong.", await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task APasswordAloneDoesNotSignInATwoFactorAccount()
    {
        const string email = "two-factor@bigfatfile.local";
        var userId = await TestData.CreateUserAsync(origin, email);
        using (var scope = origin.Factory.Services.CreateScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<BffUser>>();
            await users.SetTwoFactorEnabledAsync((await users.FindByIdAsync(userId.ToString()))!, true);
        }

        using var browser = origin.CreateClient();
        var response = await SignIn.PostSignInFormAsync(browser, email, TestData.Password);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("two-factor sign-in", await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        Assert.Equal(HttpStatusCode.Unauthorized, (await browser.GetAsync("/api/v1/account/me", TestContext.Current.CancellationToken)).StatusCode);
    }

    [Fact]
    public async Task AnAppTokenIsNotAUser()
    {
        using var client = origin.CreateClient();
        var tokens = await SignIn.ClientCredentialsAsync(client, BffScopes.FilesRead);

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokens.GetProperty("access_token").GetString());
        var response = await client.GetAsync("/api/v1/account/me", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task TheJwksPublishesTheSigningKey()
    {
        using var client = origin.CreateClient();

        var jwks = await client.GetFromJsonAsync<JsonElement>("/.well-known/jwks", TestContext.Current.CancellationToken);

        var key = Assert.Single(jwks.GetProperty("keys").EnumerateArray());
        Assert.Equal(("EC", "P-256"), (key.GetProperty("kty").GetString(), key.GetProperty("crv").GetString()));
    }

    private static JsonElement JwtHeader(string jwt)
    {
        var header = jwt.Split('.')[0].Replace('-', '+').Replace('_', '/');
        header = header.PadRight(header.Length + (4 - header.Length % 4) % 4, '=');
        return JsonDocument.Parse(Encoding.UTF8.GetString(Convert.FromBase64String(header))).RootElement;
    }
}
