namespace BigFatFile.Server.Account;

/// <summary>
/// The JSON endpoints behind the web app's account screens. They work only on the same
/// origin, with the session cookie; Apps sign users in through <c>/connect/authorize</c>.
/// </summary>
internal static class AccountApi
{
    public static void MapAccountApi(this IEndpointRouteBuilder endpoints)
    {
        var account = endpoints.MapGroup("/account").WithTags("auth");
        account.MapSessionEndpoints();
        account.MapSignUpEndpoints();
        account.MapPasswordEndpoints();
        account.MapInvitationAcceptanceEndpoints();
        account.MapExternalSignInEndpoints();
    }
}
