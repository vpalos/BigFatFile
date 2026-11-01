using BigFatFile.Contracts.Account;
using Microsoft.AspNetCore.Http.HttpResults;

namespace BigFatFile.Server.Account;

internal static class PasswordEndpoints
{
    public static void MapPasswordEndpoints(this IEndpointRouteBuilder account)
    {
        account.MapPost("/forgot-password", ForgotPasswordAsync)
            .WithName("forgotPassword")
            .WithSummary("Request a password reset")
            .WithDescription("""
                Sends an email with a link to reset the password.

                The answer is always 202, whether or not the address has an account. This way the response does not reveal which addresses have accounts.
                """)
            .AllowAnonymous();

        account.MapPost("/reset-password", ResetPasswordAsync)
            .WithName("resetPassword")
            .WithSummary("Reset the password")
            .WithDescription("""
                Sets a new password, with the user ID and the code from the password reset email. The link in the email opens the web app, which reads both values from it.

                Errors:

                - 400 with the code `invalid_code`: the code is wrong or has expired.
                - 400 with field errors: the new password is not valid, for example shorter than 12 characters.
                """)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .AllowAnonymous();
    }

    private static Task<Accepted> ForgotPasswordAsync(EmailRequest request, CancellationToken cancellationToken) =>
        throw new NotImplementedException();

    private static Task<Results<NoContent, ValidationProblem, ProblemHttpResult>> ResetPasswordAsync(ResetPasswordRequest request, CancellationToken cancellationToken) =>
        throw new NotImplementedException();
}
