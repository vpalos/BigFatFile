using BigFatFile.Contracts.Account;
using Microsoft.AspNetCore.Http.HttpResults;

namespace BigFatFile.Server.Account;

internal static class SignUpEndpoints
{
    public static void MapSignUpEndpoints(this IEndpointRouteBuilder account)
    {
        account.MapPost("/sign-up", SignUpAsync)
            .WithName("signUp")
            .WithSummary("Sign up")
            .WithDescription("""
                Creates an account with a name, an email address and a password.

                - With `invitationToken`, the invitation email proves that the person receives mail at the address. The account is created at once, joins the invited tenant, and its session starts. The outcome is `signed_in`.
                - Without it, a confirmation email is sent. The account can sign in once the address is confirmed. The outcome is `confirmation_sent`.
                - When the address already has an account, the outcome is also `confirmation_sent`, and that account gets an email saying so. This way the response does not reveal which addresses have accounts.

                Errors:

                - 400 with field errors: a field is not valid, for example a password shorter than 12 characters.
                - 403 with the code `sign_up_closed`: this server accepts sign-ups only with an invitation, and the request has none. The one exception is the operator's address, set in `BFF_OPERATOR_EMAIL`.
                """)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .AllowAnonymous();

        account.MapPost("/confirm-email", ConfirmEmailAsync)
            .WithName("confirmEmail")
            .WithSummary("Confirm an email address")
            .WithDescription("""
                Confirms the address with the user ID and the code from the confirmation email, and starts a session. The link in the email opens the web app, which reads both values from it.

                On a server with open sign-up, this is also when the person's new tenant is created.

                Answers 400 with the code `invalid_code` when the code is wrong or has expired.
                """)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .AllowAnonymous();

        account.MapPost("/resend-confirmation", ResendConfirmationAsync)
            .WithName("resendConfirmation")
            .WithSummary("Resend the confirmation email")
            .WithDescription("""
                Sends the confirmation email again, to an address that is not confirmed yet.

                The answer is always 202, whether or not the address has an account. This way the response does not reveal which addresses have accounts.
                """)
            .AllowAnonymous();
    }

    private static Task<Results<Ok<SignUpResult>, ValidationProblem, ProblemHttpResult>> SignUpAsync(SignUpRequest request, HttpContext context) =>
        throw new NotImplementedException();

    private static Task<Results<NoContent, ProblemHttpResult>> ConfirmEmailAsync(ConfirmEmailRequest request, HttpContext context) =>
        throw new NotImplementedException();

    private static Task<Accepted> ResendConfirmationAsync(EmailRequest request, CancellationToken cancellationToken) =>
        throw new NotImplementedException();
}
