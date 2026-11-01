namespace BigFatFile.Contracts.Account;

/// <summary>A write made with the session cookie carries this token in the <c>X-CSRF-Token</c> header.</summary>
public sealed record CsrfToken(string Token);
