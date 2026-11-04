namespace BigFatFile.Contracts.Api;

/// <summary>
/// The <c>code</c> of every problem response. A client branches on the code, never on the
/// title, which may change.
/// </summary>
public static class ErrorCodes
{
    public const string BadRequest = "bad_request";
    public const string InvalidFields = "invalid_fields";
    public const string AuthenticationRequired = "authentication_required";
    public const string Forbidden = "forbidden";
    public const string NotFound = "not_found";
    public const string MethodNotAllowed = "method_not_allowed";
    public const string RequestTooLarge = "request_too_large";
    public const string UnsupportedMediaType = "unsupported_media_type";
    public const string ServerError = "server_error";

    public const string CsrfTokenInvalid = "csrf_token_invalid";
    public const string UserRequired = "user_required";
    public const string AdminRequired = "admin_required";
    public const string MemberRequired = "member_required";
    public const string LastAdmin = "last_admin";
    public const string AlreadyMember = "already_member";
    public const string TooManyRequests = "too_many_requests";

    public const string SignUpClosed = "sign_up_closed";
    public const string WrongCredentials = "wrong_credentials";
    public const string EmailNotConfirmed = "email_not_confirmed";
    public const string LockedOut = "locked_out";
    public const string InvalidCode = "invalid_code";
    public const string AlreadyConfirmed = "already_confirmed";
    public const string WrongAccount = "wrong_account";
}
