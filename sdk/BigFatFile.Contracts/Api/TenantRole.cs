using System.Text.Json.Serialization;

namespace BigFatFile.Contracts.Api;

/// <summary>
/// What a member may do in a tenant. Permission rules on drives and folders apply on top.
/// - <c>admin</c>: manages members, groups, drives and the tenant's settings, and is owner of every drive.
/// - <c>member</c>: may create drives, and sees the member list and the groups they are in.
/// - <c>guest</c>: has access only through the permission rules that name them, and sees neither the member list nor the groups.
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
