using BigFatFile.Contracts.Account;
using Microsoft.AspNetCore.Http.HttpResults;

namespace BigFatFile.Server.Account;

internal static class SignUpEndpoints
{
    public static void MapSignUpEndpoints(this IEndpointRouteBuilder account)
    {
        account.MapPost("/sign-up", SignUpAsync)
            .WithName("signUp")
            .WithSummary("Creates an account. Without an invitation token, a confirmation email is sent first.")
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .AllowAnonymous();

        account.MapPost("/confirm-email", ConfirmEmailAsync)
            .WithName("confirmEmail")
            .WithSummary("Confirms the address with the code from the confirmation email, and starts a session.")
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .AllowAnonymous();

        account.MapPost("/resend-confirmation", ResendConfirmationAsync)
            .WithName("resendConfirmation")
            .WithSummary("Sends the confirmation email again. The answer is the same whether or not the address has an account.")
            .AllowAnonymous();
    }

    private static Task<Results<Ok<SignUpResult>, ValidationProblem, ProblemHttpResult>> SignUpAsync(SignUpRequest request, HttpContext context) =>
        throw new NotImplementedException();

    private static Task<Results<NoContent, ProblemHttpResult>> ConfirmEmailAsync(ConfirmEmailRequest request, HttpContext context) =>
        throw new NotImplementedException();

    private static Task<Accepted> ResendConfirmationAsync(EmailRequest request, CancellationToken cancellationToken) =>
        throw new NotImplementedException();
}
