using Microsoft.AspNetCore.Http.HttpResults;

namespace BigFatFile.Server.Api;

/// <summary>Problem responses (RFC 9457) with the stable <c>type</c> and <c>code</c> of <c>Architecture.API.md</c>, section 2.</summary>
internal static class Problems
{
    public static ProblemHttpResult Of(int status, string code, string title) =>
        TypedResults.Problem(
            statusCode: status,
            title: title,
            type: "https://bigfatfile.com/problems/" + code.Replace('_', '-'),
            extensions: new Dictionary<string, object?> { ["code"] = code });
}
