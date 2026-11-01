namespace BigFatFile.Contracts.Account;

/// <param name="TenantId">The tenant to scope the session to. Without it, the session gets the tenant the user signed in to last.</param>
public sealed record SignInRequest(string Email, string Password, Guid? TenantId = null);

public sealed record SwitchTenantRequest(Guid TenantId);
