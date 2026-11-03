using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using BigFatFile.Origin.Server.Development;
using Microsoft.AspNetCore.WebUtilities;

namespace BigFatFile.Origin.Tests;

/// <summary>The steps a CLI goes through: sign in in the browser, then authorization code with PKCE.</summary>
internal static partial class SignIn
{
    public static async Task SignInWithPasswordAsync(HttpClient browser, string email, string password)
    {
        var response = await PostSignInFormAsync(browser, email, password);
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
    }

    /// <summary>Sends the sign-in form. A redirect means signed in; the form again means refused.</summary>
    public static async Task<HttpResponseMessage> PostSignInFormAsync(HttpClient browser, string email, string password) =>
        await browser.PostAsync("/account/sign-in", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = await CsrfTokenAsync(browser),
            ["email"] = email,
            ["password"] = password,
            ["returnUrl"] = "/",
        }));

    /// <summary>The request token of the browser's antiforgery cookie, as the sign-in form carries it.</summary>
    public static async Task<string> CsrfTokenAsync(HttpClient browser)
    {
        var form = await browser.GetStringAsync("/account/sign-in");
        return WebUtility.HtmlDecode(AntiforgeryField().Match(form).Groups[1].Value);
    }

    public static async Task<JsonElement> AuthorizationCodeAsync(HttpClient browser, string scope)
    {
        var verifier = Base64Url(RandomNumberGenerator.GetBytes(32));
        var challenge = Base64Url(SHA256.HashData(Encoding.ASCII.GetBytes(verifier)));

        var authorize = await browser.GetAsync(QueryHelpers.AddQueryString("/connect/authorize", new Dictionary<string, string?>
        {
            ["client_id"] = DevSeed.CliClientId,
            ["redirect_uri"] = DevSeed.CliRedirectUri,
            ["response_type"] = "code",
            ["scope"] = scope,
            ["state"] = "test-state",
            ["code_challenge"] = challenge,
            ["code_challenge_method"] = "S256",
        }));
        Assert.Equal(HttpStatusCode.Redirect, authorize.StatusCode);
        var callback = authorize.Headers.Location!;
        Assert.StartsWith(DevSeed.CliRedirectUri, callback.ToString());
        var query = QueryHelpers.ParseQuery(callback.Query);
        Assert.Equal("test-state", query["state"].ToString());

        var token = await browser.PostAsync("/connect/token", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "authorization_code",
            ["client_id"] = DevSeed.CliClientId,
            ["redirect_uri"] = DevSeed.CliRedirectUri,
            ["code"] = query["code"].ToString(),
            ["code_verifier"] = verifier,
        }));
        Assert.Equal(HttpStatusCode.OK, token.StatusCode);
        return await token.Content.ReadFromJsonAsync<JsonElement>();
    }

    public static async Task<JsonElement> ClientCredentialsAsync(HttpClient client, string scope)
    {
        var token = await client.PostAsync("/connect/token", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "client_credentials",
            ["client_id"] = DevSeed.AppClientId,
            ["client_secret"] = DevSeed.AppClientSecret,
            ["scope"] = scope,
        }));
        Assert.Equal(HttpStatusCode.OK, token.StatusCode);
        return await token.Content.ReadFromJsonAsync<JsonElement>();
    }

    private static string Base64Url(byte[] bytes) => Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    [GeneratedRegex("name=\"__RequestVerificationToken\" value=\"([^\"]+)\"")]
    private static partial Regex AntiforgeryField();
}
