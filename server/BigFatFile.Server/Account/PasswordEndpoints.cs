using BigFatFile.Contracts.Account;
using Microsoft.AspNetCore.Http.HttpResults;

namespace BigFatFile.Server.Account;

internal static class PasswordEndpoints
{
    public static void MapPasswordEndpoints(this IEndpointRouteBuilder account)
    {
        account.MapPost("/forgot-password", ForgotPasswordAsync)
            .WithName("forgotPassword")
            .WithSummary("Sends a password reset email. The answer is the same whether or not the address has an account.")
            .AllowAnonymous();

        account.MapPost("/reset-password", ResetPasswordAsync)
            .WithName("resetPassword")
            .WithSummary("Sets a new password with the code from the password reset email.")
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .AllowAnonymous();
    }

    private static Task<Accepted> ForgotPasswordAsync(EmailRequest request, CancellationToken cancellationToken) =>
        throw new NotImplementedException();

    private static Task<Results<NoContent, ValidationProblem, ProblemHttpResult>> ResetPasswordAsync(ResetPasswordRequest request, CancellationToken cancellationToken) =>
        throw new NotImplementedException();
}
