using System.Text.Json.Serialization;

namespace BigFatFile.Contracts.Api;

/// <summary>One entry of a tenant's audit log.</summary>
/// <param name="Actor">The user who acted. Null when no signed-in user did it.</param>
/// <param name="AgentClientId">The client ID of the App or the CLI that acted for the user. Null when the user acted alone.</param>
/// <param name="IpAddress">The address that the request came from.</param>
/// <param name="UserAgent">The name that the browser or the program sent in its <c>User-Agent</c> header, cut to 512 characters.</param>
public sealed record AuditEvent(
    Guid Id,
    DateTimeOffset OccurredAt,
    AuditAction Action,
    AuditActor? Actor,
    string? AgentClientId,
    string? IpAddress,
    string? UserAgent);

/// <param name="Email">Null once the user's account is deleted.</param>
/// <param name="Name">Null once the user's account is deleted.</param>
public sealed record AuditActor(Guid UserId, string? Email, string? Name);

/// <summary>
/// - <c>signed_in</c>: the user signed in with a password, and a session started.
/// - <c>app_authorized</c>: an App or the CLI received tokens to act for the user. <c>agentClientId</c> names it.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<AuditAction>))]
public enum AuditAction
{
    [JsonStringEnumMemberName("signed_in")]
    SignedIn,

    [JsonStringEnumMemberName("app_authorized")]
    AppAuthorized,
}
