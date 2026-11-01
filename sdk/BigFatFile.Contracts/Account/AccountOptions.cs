using System.Text.Json.Serialization;

namespace BigFatFile.Contracts.Account;

/// <summary>What the sign-in and sign-up screens offer on this server.</summary>
/// <param name="Providers">The external accounts a person can sign in with, in display order.</param>
public sealed record AccountOptions(SignUpPolicy SignUp, IReadOnlyList<SignInProvider> Providers);

/// <summary><c>invitation</c>: only people with an invitation can create an account. <c>open</c>: anyone can create an account, and gets a new tenant of their own.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<SignUpPolicy>))]
public enum SignUpPolicy
{
    [JsonStringEnumMemberName("invitation")]
    Invitation,

    [JsonStringEnumMemberName("open")]
    Open,
}

/// <param name="Key">The provider's path segment in <c>/account/external/{provider}</c>.</param>
/// <param name="Name">The name to show on the button.</param>
public sealed record SignInProvider(string Key, string Name);
