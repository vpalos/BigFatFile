using BigFatFile.Contracts.Api;

namespace BigFatFile.Contracts.Account;

/// <param name="Token">The token from the invitation email.</param>
public sealed record InvitationTokenRequest(string Token);

/// <summary>
/// What the invitation screen shows before the person accepts.
/// </summary>
/// <param name="AccountExists">True when the invited address already has an account, so the screen asks the person to sign in rather than sign up.</param>
public sealed record InvitationPreview(string TenantName, string InvitedBy, string Email, TenantRole Role, bool AccountExists);
