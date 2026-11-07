namespace BigFatFile.Contracts.Account;

/// <summary>
/// Write operations that use the session cookie send this token in the <c>X-CSRF-Token</c> header.
/// </summary>
public sealed record CsrfToken(string Token);
