namespace BigFatFile.Contracts.Api;

/// <summary>The signed-in user and the tenants they belong to.</summary>
/// <param name="TenantId">The tenant this token or session is scoped to; null when it is scoped to none.</param>
public sealed record Me(Guid Id, string Email, string Name, Guid? TenantId, IReadOnlyList<MeWithinTenant> Tenants);

public sealed record MeWithinTenant(Guid Id, string Name, TenantRole Role);
