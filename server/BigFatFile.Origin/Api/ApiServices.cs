using System.Text.Json.Serialization;
using BigFatFile.Contracts.Api;
using BigFatFile.Origin.Api.Errors;
using BigFatFile.Origin.Api.Parameters;
using BigFatFile.Origin.Auth;
using BigFatFile.Origin.Auth.Tokens;
using Microsoft.AspNetCore.Authorization;
using Microsoft.OpenApi;
using Scalar.AspNetCore;

namespace BigFatFile.Origin.Api;

internal static class ApiServices
{
    public const string DocumentPath = "/api/v1/openapi.json";

    private static readonly Dictionary<string, string> TagDescriptions = new()
    {
        ["account"] = """
            The signed-in user, and the endpoints behind the web app's account screens: sign-up, sign-in, passwords and invitations.

            - The web app calls them from the same origin as the server, with the session cookie.
            - `GET /api/v1/account/me` also accepts a token with the `openid` scope, so Apps can call it too. The other endpoints do not accept tokens.
            - Apps get their tokens from the OpenID Connect endpoints under `/connect`.
            """,
        ["meta"] = "Information about the server itself.",
        ["tenants"] = """
            The tenant, its members, its invitations and its audit log.

            - A token or a session works in one tenant. A request with any other tenant's ID gets 404, the same answer as for a tenant that does not exist.
            - The endpoints that need the `members.manage` or the `tenant.admin` scope are for admins only. A user who is not an admin of the tenant gets 403 with the code `admin_required`.
            - The endpoints that need the `members.read` scope are for members and admins. A guest of the tenant gets 403 with the code `member_required`.
            """,
    };

    public static IServiceCollection AddApi(this IServiceCollection services)
    {
        services.AddProblemDetails(options => options.CustomizeProblemDetails = Problems.FillInCode);

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
                    Description = "The one public API of a BigFatFile Origin.\n\n" + ApiErrorsOpenApi.ErrorsSection(),
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

                // The scope handler lets the session cookie through, except for token-only scopes.
                var session = scopes.Count > 0 && !scopes.Any(BffScopes.TokenOnly.Contains);
                if (session || policies.Contains(AuthServices.SessionPolicy))
                {
                    operation.Security ??= [];
                    operation.Security.Add(new OpenApiSecurityRequirement
                    {
                        [new OpenApiSecuritySchemeReference("session", context.Document)] = [],
                    });
                }

                return Task.CompletedTask;
            });
            options.AddEnumParameters();
            options.AddApiErrors();
        });
        return services;
    }

    private static OpenApiSecurityScheme SessionScheme() => new()
    {
        Type = SecuritySchemeType.ApiKey,
        In = ParameterLocation.Cookie,
        Name = "bff_session",
        Description = "The web app's session, on the same origin only. Write operations that use it must also send the X-CSRF-Token header, with the token from GET /api/v1/account/csrf. A write operation without that header gets 400 with the code csrf_token_invalid.",
    };

    private static OpenApiSecurityScheme OAuthScheme() => new()
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
                Scopes = BffScopes.Descriptions.ToDictionary(),
            },
        },
    };
}
