using System.Text.Json.Serialization;

namespace BigFatFile.Contracts.Api;

/// <summary>
/// What a member may do in a tenant. Node permissions apply on top.
/// - <c>admin</c>: manages members, invitations and the tenant's settings.
/// - <c>member</c>: a member without the rights of an admin.
/// - <c>guest</c>: an outside person who sees only what is shared with them.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<TenantRole>))]
public enum TenantRole
{
    [JsonStringEnumMemberName("admin")]
    Admin,

    [JsonStringEnumMemberName("member")]
    Member,

    [JsonStringEnumMemberName("guest")]
    Guest,
}
