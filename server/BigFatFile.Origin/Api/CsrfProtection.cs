using BigFatFile.Origin.Api.Errors;
using BigFatFile.Origin.Auth;
using Microsoft.AspNetCore.Antiforgery;

namespace BigFatFile.Origin.Api;

/// <summary>
/// Refuses a write operation that uses the session cookie and does not send a valid
/// <c>X-CSRF-Token</c> header. A request with a bearer token passes, because another website
/// cannot make the browser add a token to a request.
/// </summary>
internal static class CsrfProtection
{
    public static RouteGroupBuilder RequireCsrfToken(this RouteGroupBuilder group)
    {
        ((IEndpointConventionBuilder)group).Add(endpoint =>
        {
            if (endpoint.Metadata.OfType<HttpMethodMetadata>().Any(metadata => metadata.HttpMethods.Any(IsWrite)))
            {
                endpoint.Metadata.Add(new ApiErrorMetadata(ApiErrors.CsrfTokenInvalid, Note: null));
            }
        });
        return group.AddEndpointFilter(CheckAsync);
    }

    private static async ValueTask<object?> CheckAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var http = context.HttpContext;
        if (IsWrite(http.Request.Method)
            && AuthServices.UsesSession(http.User)
            && !await http.RequestServices.GetRequiredService<IAntiforgery>().IsRequestValidAsync(http))
        {
            return Problems.Of(ApiErrors.CsrfTokenInvalid);
        }

        return await next(context);
    }

    private static bool IsWrite(string method) =>
        !(HttpMethods.IsGet(method) || HttpMethods.IsHead(method) || HttpMethods.IsOptions(method) || HttpMethods.IsTrace(method));
}
