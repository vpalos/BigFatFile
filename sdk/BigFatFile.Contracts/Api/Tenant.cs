namespace BigFatFile.Contracts.Api;

/// <param name="Plan">The plan's key. The open core has one plan, <c>unlimited</c>.</param>
public sealed record Tenant(Guid Id, string Name, string Plan, DateTimeOffset CreatedAt);
