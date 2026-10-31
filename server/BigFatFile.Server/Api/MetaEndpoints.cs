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
            .WithSummary("What a client needs to know about this server: versions, chunker parameters and enabled features.")
            .AllowAnonymous();
}
