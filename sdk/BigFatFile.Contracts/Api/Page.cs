namespace BigFatFile.Contracts.Api;

/// <summary>One page of a listing.</summary>
/// <param name="NextCursor">Pass it as <c>cursor</c> to get the next page; null on the last page.</param>
public sealed record Page<T>(IReadOnlyList<T> Items, string? NextCursor);
