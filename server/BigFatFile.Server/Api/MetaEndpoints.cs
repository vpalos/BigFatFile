using BigFatFile.Contracts.Api;
using BigFatFile.Contracts.Chunking;
using BigFatFile.Contracts.Names;
using BigFatFile.Server.Features;
using BigFatFile.Server.Hosting;

namespace BigFatFile.Server.Api;

internal static class MetaEndpoints
{
    public static void MapMetaEndpoints(this IEndpointRouteBuilder api) =>
        api.MapGet("/meta", (LicenceFeatureGate features) => new Meta(
                ApiVersion.Current,
                OriginHost.ServerVersion,
                new MetaChunker(
                    ChunkerParameters.Algorithm,
                    ChunkerParameters.MinSize,
                    ChunkerParameters.AverageSize,
                    ChunkerParameters.MaxSize,
                    ChunkerParameters.NormalizationLevel),
                NodeNames.UnicodeVersion,
                features.EnabledKeys))
            .WithTags("meta")
            .WithName("getMeta")
            .WithSummary("Get server information")
            .WithDescription("""
                Returns what a client needs to know about this server before it uses the API:

                - the API version and the server version;
                - the parameters of the chunker, which splits files into chunks for transfer;
                - the Unicode version used to normalise file names before they are compared;
                - the licensed features that are turned on.

                It needs no token.
                """)
            .AllowAnonymous();
}
