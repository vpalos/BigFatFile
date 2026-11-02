using BigFatFile.Contracts.Account;
using BigFatFile.Contracts.Api;
using BigFatFile.Contracts.Chunking;
using BigFatFile.Contracts.Names;
using BigFatFile.Server.Features;
using BigFatFile.Server.Hosting;

namespace BigFatFile.Server.Api.Endpoints;

internal static class MetaEndpoints
{
    public static void MapMetaEndpoints(this IEndpointRouteBuilder meta) =>
        meta.MapGet("", (LicenceFeatureGate features) => new Meta(
                ApiVersion.Current,
                OriginHost.ServerVersion,
                new MetaChunker(
                    ChunkerParameters.Algorithm,
                    ChunkerParameters.MinSize,
                    ChunkerParameters.AverageSize,
                    ChunkerParameters.MaxSize,
                    ChunkerParameters.NormalizationLevel),
                NodeNames.UnicodeVersion,
                features.EnabledKeys,
                // No setting chooses these yet: the sign-up policy is its default, password
                // sign-in is on, and no provider is set up.
                new AccountOptions(SignUpPolicy.Invitation, Password: true, [])))
            .WithName("getMeta")
            .WithSummary("Get server information")
            .WithDescription("""
                Returns what a client needs to know about this server before it uses the API:

                - the API version and the server version;
                - the parameters of the chunker, which splits files into chunks for transfer;
                - the Unicode version used to normalise file names before they are compared;
                - the licensed features that are turned on;
                - `account`: what the sign-in and sign-up flows offer:
                    - `signUp` says whether anyone can create an account, or only people with an invitation;
                    - `password` says whether people can sign in and sign up with an email address and a password;
                    - `providers` lists the external accounts that people can sign in with, such as Google or Microsoft.

                It needs no token.
                """)
            .AllowAnonymous();
}
