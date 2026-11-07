namespace BigFatFile.Contracts.Api;

/// <summary>
/// An email address invited to join a tenant, until the person accepts or the invitation expires.
/// </summary>
public sealed record Invitation(Guid Id, string Email, TenantRole Role, Guid InvitedBy, DateTimeOffset CreatedAt, DateTimeOffset ExpiresAt);

/// <param name="Role">Admin or member. Guests are invited through sharing.</param>
public sealed record CreateInvitationRequest(string Email, TenantRole Role);
