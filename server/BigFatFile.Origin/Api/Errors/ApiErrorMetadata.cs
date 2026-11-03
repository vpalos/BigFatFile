namespace BigFatFile.Origin.Api.Errors;

/// <param name="Note">What this endpoint adds to the error's description, as full sentences.</param>
internal sealed record ApiErrorMetadata(ApiError Error, string? Note);

internal static class ApiErrorMetadataExtensions
{
    /// <summary>Declares an error that the endpoint, or every endpoint of the group, can return.</summary>
    /// <param name="note">Only when this endpoint has something to add to the error's description.</param>
    public static TBuilder ProducesError<TBuilder>(this TBuilder builder, ApiError error, string? note = null)
        where TBuilder : IEndpointConventionBuilder =>
        builder.WithMetadata(new ApiErrorMetadata(error, note));
}
