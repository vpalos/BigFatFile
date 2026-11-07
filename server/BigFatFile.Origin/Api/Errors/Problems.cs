using Microsoft.AspNetCore.Http.HttpResults;

namespace BigFatFile.Origin.Api.Errors;

/// <summary>
/// Problem responses (RFC 9457) built from the definitions in <see cref="ApiErrors"/>.
/// </summary>
internal static class Problems
{
    public const string CodeField = "code";

    /// <param name="detail">A fact about this one occurrence. The title is the same for every occurrence.</param>
    public static ProblemHttpResult Of(ApiError error, string? detail = null) =>
        TypedResults.Problem(
            statusCode: error.Status,
            title: error.Title,
            type: error.Type,
            detail: detail,
            extensions: new Dictionary<string, object?> { [CodeField] = error.Code });

    public static ProblemHttpResult InvalidFields(IDictionary<string, string[]> errors) =>
        TypedResults.Problem(new HttpValidationProblemDetails(errors)
        {
            Status = ApiErrors.InvalidFields.Status,
            Title = ApiErrors.InvalidFields.Title,
            Type = ApiErrors.InvalidFields.Type,
            Extensions = { [CodeField] = ApiErrors.InvalidFields.Code },
        });

    /// <summary>
    /// Gives a code to the errors that ASP.NET Core writes itself, such as 404 for an unknown address.
    /// </summary>
    public static void FillInCode(ProblemDetailsContext context)
    {
        var problem = context.ProblemDetails;
        if (problem.Extensions.ContainsKey(CodeField))
        {
            return;
        }

        var error = problem is HttpValidationProblemDetails
            ? ApiErrors.InvalidFields
            : ApiErrors.ForStatus(problem.Status ?? context.HttpContext.Response.StatusCode);
        problem.Type = error.Type;
        problem.Title = error.Title;
        problem.Extensions[CodeField] = error.Code;
    }
}
