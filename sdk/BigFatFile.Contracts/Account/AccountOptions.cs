using System.Text.Json.Serialization;

namespace BigFatFile.Contracts.Account;

/// <summary>
/// What the sign-in and sign-up flows offer on this server.
/// </summary>
/// <param name="Password">Whether people can sign in, and sign up, with an email address and a password. When it is false, they use the external providers only.</param>
/// <param name="Providers">The external accounts a person can sign in with, in display order.</param>
public sealed record AccountOptions(SignUpPolicy SignUp, bool Password, IReadOnlyList<SignInProvider> Providers);

/// <summary>
/// - <c>invitation</c>: only people with an invitation can create an account.
/// - <c>open</c>: anyone can create an account, and gets a new tenant of their own.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<SignUpPolicy>))]
public enum SignUpPolicy
{
    [JsonStringEnumMemberName("invitation")]
    Invitation,

    [JsonStringEnumMemberName("open")]
    Open,
}

/// <param name="Key">The provider's path segment in <c>/api/v1/account/sign-in/{provider}</c>.</param>
/// <param name="Name">The name to show on the button.</param>
public sealed record SignInProvider(ExternalProvider Key, string Name);

/// <summary>
/// - <c>google</c>: a Google account.
/// - <c>microsoft</c>: a Microsoft account.
/// - <c>oidc</c>: an account at the one OpenID Connect provider that the server's operator set up, such as Keycloak or Authentik.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<ExternalProvider>))]
public enum ExternalProvider
{
    [JsonStringEnumMemberName("google")]
    Google,

    [JsonStringEnumMemberName("microsoft")]
    Microsoft,

    [JsonStringEnumMemberName("oidc")]
    Oidc,
}
