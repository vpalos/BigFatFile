namespace BigFatFile.Contracts.Account;

/// <param name="Code">The code from the confirmation email.</param>
public sealed record ConfirmEmailRequest(Guid UserId, string Code);

/// <summary>An email address, for requests that send an email to it.</summary>
public sealed record EmailRequest(string Email);
