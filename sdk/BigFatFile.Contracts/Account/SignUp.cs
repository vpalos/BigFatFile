using System.Text.Json.Serialization;

namespace BigFatFile.Contracts.Account;

/// <param name="InvitationToken">The token from an invitation email. It proves the address, so no confirmation email is sent.</param>
public sealed record SignUpRequest(string Name, string Email, string Password, string? InvitationToken = null);

public sealed record SignUpResult(SignUpOutcome Outcome);

/// <summary><c>confirmation_sent</c>: a confirmation email is on its way. It is also the answer when the address already has an account, so the response does not reveal which addresses do. <c>signed_in</c>: the invitation proved the address, the account exists and its session has started.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<SignUpOutcome>))]
public enum SignUpOutcome
{
    [JsonStringEnumMemberName("confirmation_sent")]
    ConfirmationSent,

    [JsonStringEnumMemberName("signed_in")]
    SignedIn,
}
