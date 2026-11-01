using System.Text.Json.Serialization;
using BigFatFile.Contracts.Api;
using BigFatFile.Server.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.OpenApi;
using Scalar.AspNetCore;

namespace BigFatFile.Server.Api;

internal static class ApiServices
{
    public const string DocumentPath = "/api/v1/openapi.json";

    private static readonly Dictionary<string, string> TagDescriptions = new()
    {
        ["auth"] = """
            The endpoints behind the web app's account screens: sign-up, sign-in, passwords and invitations.

            - Only the web app calls them, from the same origin as the server.
            - They work with the session cookie, not with tokens.
            - Apps get their tokens from the OpenID Connect endpoints under `/connect` instead.
            """,
        ["meta"] = "Information about the server itself.",
        ["tenant"] = """
            The signed-in user, the tenant, its members and its invitations.

            - A token or a session works in one tenant. A request with any other tenant's ID gets 404, the same answer as for a tenant that does not exist.
            - The endpoints that need the `members.manage` scope are for admins only. A user who is not an admin of the tenant gets 403 with the code `admin_required`.
            """,
    };

    public static IServiceCollection AddApi(this IServiceCollection services)
    {
        services.AddProblemDetails();

        // Numbers must be JSON numbers. The web defaults also accept them as strings, and the
        // spec would then type every integer as "integer or string".
        services.ConfigureHttpJsonOptions(options => options.SerializerOptions.NumberHandling = JsonNumberHandling.Strict);
        services.AddOpenApi("v1", options =>
        {
            options.AddDocumentTransformer((document, _, _) =>
            {
                document.Info = new OpenApiInfo
                {
                    Title = "BigFatFile API",
                    Version = ApiVersion.Current,
                    Description = "The one public API of a BigFatFile Origin.",
                };
                foreach (var tag in document.Tags ?? Enumerable.Empty<OpenApiTag>())
                {
                    tag.Description = TagDescriptions.GetValueOrDefault(tag.Name ?? "");
                }
                document.Components ??= new OpenApiComponents();
                document.Components.SecuritySchemes ??= new Dictionary<string, IOpenApiSecurityScheme>();
                document.Components.SecuritySchemes["oauth2"] = OAuthScheme();
                document.Components.SecuritySchemes["session"] = SessionScheme();
                return Task.CompletedTask;
            });
            options.AddOperationTransformer((operation, context, _) =>
            {
                var policies = context.Description.ActionDescriptor.EndpointMetadata
                    .OfType<IAuthorizeData>()
                    .Select(data => data.Policy)
                    .ToList();
                var scopes = policies.Select(ScopeAuthorization.ScopeOf).OfType<string>().ToList();
                if (scopes.Count > 0)
                {
                    operation.Security ??= [];
                    operation.Security.Add(new OpenApiSecurityRequirement
                    {
                        [new OpenApiSecuritySchemeReference("oauth2", context.Document)] = scopes,
                    });
                }

                // The scope handler lets the session cookie through, so every scoped endpoint also accepts it.
                if (scopes.Count > 0 || policies.Contains(AuthServices.SessionPolicy))
                {
                    operation.Security ??= [];
                    operation.Security.Add(new OpenApiSecurityRequirement
                    {
                        [new OpenApiSecuritySchemeReference("session", context.Document)] = [],
                    });
                }

                return Task.CompletedTask;
            });
        });
        return services;
    }

    public static void MapApiDocs(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapOpenApi(DocumentPath);
        endpoints.MapScalarApiReference("/api/docs", options => options
            .WithTitle("BigFatFile API")
            .WithOpenApiRoutePattern(DocumentPath));
    }

    private static OpenApiSecurityScheme SessionScheme() => new()
    {
        Type = SecuritySchemeType.ApiKey,
        In = ParameterLocation.Cookie,
        Name = "bff_session",
        Description = "The web app's session, on the same origin only. Write operations that use it must also send the X-CSRF-Token header, with the token from GET /account/csrf. A write operation without that header gets 400 with the code csrf_token_invalid.",
    };

    private static OpenApiSecurityScheme OAuthScheme()
    {
        var scopes = BffScopes.Descriptions.ToDictionary();
        return new OpenApiSecurityScheme
        {
            Type = SecuritySchemeType.OAuth2,
            Description = "Tokens from this server's OpenID Connect endpoints. The web app on the same origin uses its session cookie instead.",
            Flows = new OpenApiOAuthFlows
            {
                AuthorizationCode = new OpenApiOAuthFlow
                {
                    AuthorizationUrl = new Uri("/connect/authorize", UriKind.Relative),
                    TokenUrl = new Uri("/connect/token", UriKind.Relative),
                    RefreshUrl = new Uri("/connect/token", UriKind.Relative),
                    Scopes = scopes,
                },
                ClientCredentials = new OpenApiOAuthFlow
                {
                    TokenUrl = new Uri("/connect/token", UriKind.Relative),
                    Scopes = scopes,
                },
            },
        };
    }
}
