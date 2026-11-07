using System.Globalization;
using System.Text;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace BigFatFile.Origin.Api.Errors;

/// <summary>
/// Puts the definitions of <see cref="ApiErrors"/> in the spec: the list of every error, the
/// fields of an error body, and the errors of each endpoint.
/// </summary>
internal static class ApiErrorsOpenApi
{
    private const string ContentType = "application/problem+json";

    // The example from the W3C Trace Context specification.
    private const string SampleTraceId = "00-0af7651916cd43dd8448eb211c80319c-b7ad6b7169203331-01";

    public static OpenApiOptions AddApiErrors(this OpenApiOptions options) =>
        options
            .AddSchemaTransformer(DescribeBodyFields)
            .AddOperationTransformer(DescribeErrorsAsync);

    /// <summary>
    /// The "Errors" section of the API's description.
    /// </summary>
    public static string ErrorsSection()
    {
        var text = new StringBuilder("""
            ## Errors

            Every error response has a JSON body in the problem details format (RFC 9457), with the content type `application/problem+json`. The `ProblemDetails` schema describes its fields. A client branches on `code`, never on `title`.

            ### Errors that any endpoint can return


            """);
        foreach (var error in ApiErrors.Generic)
        {
            text.Append(ListItem(error));
        }

        text.Append("""

            ### Errors of specific endpoints

            Each endpoint lists its own errors under its responses, with an example body for each code. For example, see the responses of [Accept an invitation](#tag/account/POST/api/v1/account/invitations/accept).
            """);
        return text.ToString();
    }

    private static string ListItem(ApiError error) =>
        $"- `{error.Code}`, {error.Status}: {error.Title} {error.Description}\n";

    private static string Explain(ApiErrorMetadata declared) =>
        $"`{declared.Error.Code}`: {declared.Error.Title} {declared.Error.Description}" + (declared.Note is null ? "" : " " + declared.Note);

    private static Task DescribeBodyFields(OpenApiSchema schema, OpenApiSchemaTransformerContext context, CancellationToken cancellationToken)
    {
        var type = context.JsonTypeInfo.Type;
        if (context.JsonPropertyInfo is not null || (type != typeof(ProblemDetails) && type != typeof(HttpValidationProblemDetails)))
        {
            return Task.CompletedTask;
        }

        schema.Properties ??= new Dictionary<string, IOpenApiSchema>();
        Describe(schema, "type", "Identifies the error, as a URN: `urn:bigfatfile:problem:` followed by the code.", required: true);
        Describe(schema, "title", "A short text for people. It may change, so a client never compares it.", required: true);
        Describe(schema, "status", "The HTTP status.", required: true);
        Describe(schema, "errors", $"Only with `{ApiErrors.InvalidFields.Code}`: what is wrong with each field.", required: false);
        schema.Properties[Problems.CodeField] = new OpenApiSchema
        {
            Type = JsonSchemaType.String,
            Description = "Identifies the error. A client branches on it.",
            Enum = [.. ApiErrors.All.Select(error => JsonValue.Create(error.Code)!)],
        };
        schema.Properties["traceId"] = new OpenApiSchema
        {
            Type = JsonSchemaType.String,
            Description = "Identifies the request in the server's traces and logs. Give it to the server's operator when you report a problem.",
        };
        schema.Required ??= new HashSet<string>();
        schema.Required.Add(Problems.CodeField);
        return Task.CompletedTask;
    }

    private static void Describe(OpenApiSchema schema, string field, string description, bool required)
    {
        if (schema.Properties!.TryGetValue(field, out var property) && property is OpenApiSchema fieldSchema)
        {
            fieldSchema.Description = description;
            if (required)
            {
                fieldSchema.Type &= ~JsonSchemaType.Null;
                schema.Required ??= new HashSet<string>();
                schema.Required.Add(field);
            }
        }
    }

    private static async Task DescribeErrorsAsync(OpenApiOperation operation, OpenApiOperationTransformerContext context, CancellationToken cancellationToken)
    {
        // An endpoint may declare again an error of its group, to add a note. The last declaration wins.
        var statuses = context.Description.ActionDescriptor.EndpointMetadata
            .OfType<ApiErrorMetadata>()
            .GroupBy(declared => declared.Error)
            .Select(declarations => declarations.Last())
            .GroupBy(declared => declared.Error.Status)
            .OrderBy(group => group.Key)
            .ToList();
        if (statuses.Count == 0)
        {
            return;
        }

        operation.Responses ??= new OpenApiResponses();
        foreach (var status in statuses)
        {
            operation.Responses.Remove(status.Key.ToString(CultureInfo.InvariantCulture));
        }

        foreach (var status in statuses)
        {
            var errors = status.ToList();
            var bodyType = errors.Any(declared => declared.Error == ApiErrors.InvalidFields)
                ? typeof(HttpValidationProblemDetails)
                : typeof(ProblemDetails);
            operation.Responses[status.Key.ToString(CultureInfo.InvariantCulture)] = new OpenApiResponse
            {
                Description = ListErrors(errors),
                Content = new Dictionary<string, OpenApiMediaType>
                {
                    [ContentType] = new()
                    {
                        Schema = await context.SchemaReferenceAsync(bodyType, cancellationToken),
                        Examples = errors.ToDictionary(
                            declared => declared.Error.Code,
                            declared => (IOpenApiExample)new OpenApiExample { Value = ExampleBody(declared.Error) }),
                    },
                },
            };
        }
    }

    private static string ListErrors(List<ApiErrorMetadata> errors)
    {
        var lines = errors.Select(Explain).ToList();
        return lines.Count == 1 ? lines[0] : string.Join("\n", lines.Select(line => "- " + line));
    }

    private static JsonObject ExampleBody(ApiError error)
    {
        var body = new JsonObject
        {
            ["type"] = error.Type,
            ["title"] = error.Title,
            ["status"] = error.Status,
            [Problems.CodeField] = error.Code,
            ["traceId"] = SampleTraceId,
        };
        if (error == ApiErrors.InvalidFields)
        {
            body["errors"] = new JsonObject { ["<field>"] = new JsonArray("<what is wrong with the field>") };
        }

        return body;
    }
}
