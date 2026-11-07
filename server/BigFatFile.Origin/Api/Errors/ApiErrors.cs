using BigFatFile.Contracts.Api;
using BigFatFile.Origin.Auth;
using BigFatFile.Origin.Auth.Users;

namespace BigFatFile.Origin.Api.Errors;

/// <param name="Description">What the error means and what a client should do. The spec shows it with each response that can return the error.</param>
internal sealed record ApiError(int Status, string Code, string Title, string Description)
{
    public string Type => "urn:bigfatfile:problem:" + Code;
}

/// <summary>
/// Every error that the API returns, each one defined once.
/// </summary>
internal static class ApiErrors
{
    public static readonly ApiError BadRequest = new(400, ErrorCodes.BadRequest,
        "The request is not valid.",
        "The server could not read the request. For example, its JSON body is malformed, a required parameter is missing, or a parameter has a value that the spec does not list. Fix the request before sending it again.");

    public static readonly ApiError InvalidFields = new(400, ErrorCodes.InvalidFields,
        "Some fields are not valid.",
        "The `errors` field lists what is wrong with each field.");

    public static readonly ApiError AuthenticationRequired = new(401, ErrorCodes.AuthenticationRequired,
        "This endpoint needs a token or a session.",
        "The request has no access token and no session cookie, or the one it has is not valid or has expired. Get a new token, or sign in again.");

    public static readonly ApiError Forbidden = new(403, ErrorCodes.Forbidden,
        "The token or the session does not allow this.",
        "The request is authenticated, but its token does not have the scope that the endpoint needs. Ask for a token with that scope.");

    public static readonly ApiError NotFound = new(404, ErrorCodes.NotFound,
        "Nothing exists at this address.",
        "The resource does not exist, or the request has no access to it. The answer is the same in both cases, so it does not reveal what exists.");

    public static readonly ApiError MethodNotAllowed = new(405, ErrorCodes.MethodNotAllowed,
        "This address does not accept this HTTP method.",
        "The endpoint exists, but not with this HTTP method. The `Allow` header lists the methods that it accepts.");

    public static readonly ApiError RequestTooLarge = new(413, ErrorCodes.RequestTooLarge,
        "The request body is too large.",
        "Send a smaller body.");

    public static readonly ApiError UnsupportedMediaType = new(415, ErrorCodes.UnsupportedMediaType,
        "This endpoint does not accept the content type of the request body.",
        "Send the body with a content type that the endpoint accepts, usually `application/json`.");

    public static readonly ApiError ServerError = new(500, ErrorCodes.ServerError,
        "The server failed to handle the request.",
        "Try again later. If the error does not go away, give the `traceId` to the server's operator.");

    public static readonly ApiError CsrfTokenInvalid = new(400, ErrorCodes.CsrfTokenInvalid,
        $"The {AuthServices.CsrfHeader} header is missing or wrong.",
        $"A write operation that uses the session cookie must send the `{AuthServices.CsrfHeader}` header, with the token from `GET /api/v1/account/csrf`. Get a new token after signing in or out, because each token belongs to one user.");

    public static readonly ApiError UserRequired = new(403, ErrorCodes.UserRequired,
        "This endpoint needs a signed-in user.",
        "The token belongs to an App, not to a user. An App gets such a token for itself, with client credentials. Use a token that a user signed in for.");

    public static readonly ApiError AdminRequired = new(403, ErrorCodes.AdminRequired,
        "Only an admin of the tenant can do this.",
        "The user is not an admin of the tenant.");

    public static readonly ApiError MemberRequired = new(403, ErrorCodes.MemberRequired,
        "A guest of the tenant cannot do this.",
        "The user is a guest of the tenant. A guest has access only through the permission rules that name them, and sees neither the member list nor the groups.");

    public static readonly ApiError LastAdmin = new(409, ErrorCodes.LastAdmin,
        "The tenant must keep at least one admin.",
        "The change would leave the tenant without an admin. Make another member an admin first.");

    public static readonly ApiError AlreadyMember = new(409, ErrorCodes.AlreadyMember,
        "This address belongs to a member of the tenant.",
        "The invited address is already the address of a member, so there is nobody new to invite.");

    public static readonly ApiError TooManyRequests = new(429, ErrorCodes.TooManyRequests,
        "Too many requests from this address. Wait for the number of seconds in the Retry-After header.",
        "This IP address sent more requests than the endpoint allows in a period of time.");

    public static readonly ApiError SignUpClosed = new(403, ErrorCodes.SignUpClosed,
        "This server accepts sign-ups only with an invitation.",
        "Ask an admin of a tenant to invite you. The operator's address, set in `BFF_OPERATOR_EMAIL`, can always sign up.");

    public static readonly ApiError WrongCredentials = new(401, ErrorCodes.WrongCredentials,
        "The email address or the password is wrong.",
        "The answer does not say which one is wrong, so that it does not reveal which addresses have accounts.");

    public static readonly ApiError EmailNotConfirmed = new(403, ErrorCodes.EmailNotConfirmed,
        "The email address is not confirmed yet.",
        "Open the link in the confirmation email first. `POST /api/v1/account/resend-confirmation` sends the email again.");

    public static readonly ApiError LockedOut = new(403, ErrorCodes.LockedOut,
        $"The account is locked for {UserServices.LockoutMinutes} minutes, because of too many wrong passwords.",
        $"The lock starts after {UserServices.LockoutAttempts} wrong passwords in a row. During the lock, even the right password gets this answer. Try again when the lock ends.");

    public static readonly ApiError InvalidCode = new(400, ErrorCodes.InvalidCode,
        "The code is wrong or has expired.",
        "Ask for a new email: a new confirmation email, or a new password reset email.");

    public static readonly ApiError AlreadyConfirmed = new(409, ErrorCodes.AlreadyConfirmed,
        "This email address is already confirmed. Sign in instead.",
        "The confirmation link was used before. Only the first confirmation starts a session, so nobody can sign in with a link that was forwarded or saved. Sign in with the password. Only a request with a valid code gets this answer, so it does not reveal which addresses are confirmed.");

    public static readonly ApiError WrongAccount = new(409, ErrorCodes.WrongAccount,
        "This invitation was sent to another email address.",
        "Sign in with the invited address instead, or ask for an invitation to the address of the signed-in user.");

    /// <summary>
    /// The errors that any endpoint can return. The server gives them to errors that it writes without a code.
    /// </summary>
    public static IReadOnlyList<ApiError> Generic { get; } =
    [
        BadRequest, InvalidFields, AuthenticationRequired, Forbidden, NotFound, MethodNotAllowed, RequestTooLarge,
        UnsupportedMediaType, ServerError,
    ];

    public static IReadOnlyList<ApiError> All { get; } =
    [
        .. Generic,
        CsrfTokenInvalid, UserRequired, AdminRequired, MemberRequired, LastAdmin, AlreadyMember, TooManyRequests,
        SignUpClosed, WrongCredentials, EmailNotConfirmed, LockedOut, InvalidCode, AlreadyConfirmed, WrongAccount,
    ];

    /// <summary>
    /// The generic error for a status that came without a code.
    /// </summary>
    public static ApiError ForStatus(int status) => status switch
    {
        400 => BadRequest,
        401 => AuthenticationRequired,
        403 => Forbidden,
        404 => NotFound,
        405 => MethodNotAllowed,
        413 => RequestTooLarge,
        415 => UnsupportedMediaType,
        < 500 => BadRequest,
        _ => ServerError,
    };
}
