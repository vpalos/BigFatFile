namespace BigFatFile.Contracts.Api;

public sealed record Member(Guid UserId, string Email, string Name, TenantRole Role, DateTimeOffset JoinedAt);

public sealed record UpdateMemberRequest(TenantRole Role);
