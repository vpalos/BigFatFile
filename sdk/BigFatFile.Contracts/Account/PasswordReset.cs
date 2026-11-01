namespace BigFatFile.Contracts.Account;

/// <param name="Code">The code from the password reset email.</param>
public sealed record ResetPasswordRequest(Guid UserId, string Code, string Password);
