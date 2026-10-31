namespace BigFatFile.Contracts.Api;

/// <summary>The signed-in user and the tenants they belong to.</summary>
public sealed record Me(Guid Id, string Email, IReadOnlyList<MeWithinTenant> Tenants);

/// <param name="Role">The user's role in the tenant: <c>admin</c>, <c>member</c> or <c>guest</c>.</param>
public sealed record MeWithinTenant(Guid Id, string Name, string Role);
