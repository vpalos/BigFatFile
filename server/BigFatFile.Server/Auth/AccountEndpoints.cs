using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace BigFatFile.Server.Auth;

/// <summary>
/// The sign-in form that the authorization endpoint sends a browser to. It is plain HTML from
/// the server, so signing in works before any web app is built.
/// </summary>
internal static class AccountEndpoints
{
    public static void MapAccountEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var account = endpoints.MapGroup("/account").ExcludeFromDescription();
        account.MapGet("/sign-in", (HttpContext context, IAntiforgery antiforgery, string? returnUrl) =>
            Page(context, antiforgery, SafeReturnUrl(returnUrl), email: "", error: null));
        account.MapPost("/sign-in", SignInAsync);
        account.MapPost("/sign-out", async (HttpContext context, IAntiforgery antiforgery, SignInManager<BffUser> signIn) =>
        {
            // Checked here: the antiforgery middleware only refuses requests to endpoints that bind form fields.
            if (!await antiforgery.IsRequestValidAsync(context))
            {
                return Results.BadRequest();
            }

            await signIn.SignOutAsync();
            return Results.Redirect("/account/sign-in");
        });
    }

    private static async Task<IResult> SignInAsync(
        HttpContext context,
        IAntiforgery antiforgery,
        UserManager<BffUser> users,
        SignInManager<BffUser> signIn,
        [FromForm] string email,
        [FromForm] string password,
        [FromForm] string? returnUrl)
    {
        var target = SafeReturnUrl(returnUrl);
        var user = await users.FindByEmailAsync(email);
        if (user is not null)
        {
            var result = await signIn.PasswordSignInAsync(user, password, isPersistent: true, lockoutOnFailure: true);
            if (result.Succeeded)
            {
                return Results.Redirect(target);
            }

            if (result.IsLockedOut)
            {
                return Page(context, antiforgery, target, email, "Too many failed attempts. Try again in a few minutes.");
            }
        }

        return Page(context, antiforgery, target, email, "The email or the password is wrong.");
    }

    // Only paths on this server, so the form cannot be used to send someone to another site.
    private static string SafeReturnUrl(string? returnUrl) =>
        returnUrl is { Length: > 0 } && returnUrl[0] == '/' && !returnUrl.StartsWith("//") && !returnUrl.StartsWith("/\\")
            ? returnUrl
            : "/";

    private static IResult Page(HttpContext context, IAntiforgery antiforgery, string returnUrl, string email, string? error)
    {
        var tokens = antiforgery.GetAndStoreTokens(context);
        var html = HtmlEncoder.Default;
        var message = error is null ? "" : $"""<p class="error">{html.Encode(error)}</p>""";
        return Results.Content(
            $$"""
            <!doctype html>
            <html lang="en">
            <head>
              <meta charset="utf-8">
              <meta name="viewport" content="width=device-width, initial-scale=1">
              <title>Sign in to BigFatFile</title>
              <style>
                body { font-family: system-ui, sans-serif; display: grid; place-items: center; min-height: 100vh; margin: 0; background: #f4f5f7; }
                form { background: #fff; padding: 2rem; border-radius: 8px; width: 20rem; box-shadow: 0 1px 4px rgb(0 0 0 / 0.1); }
                label { display: block; margin-top: 1rem; font-size: 0.9rem; }
                input { width: 100%; box-sizing: border-box; padding: 0.5rem; margin-top: 0.25rem; }
                button { margin-top: 1.5rem; width: 100%; padding: 0.6rem; }
                .error { color: #b00020; }
              </style>
            </head>
            <body>
              <form method="post" action="/account/sign-in">
                <h1>Sign in</h1>
                {{message}}
                <input type="hidden" name="{{tokens.FormFieldName}}" value="{{html.Encode(tokens.RequestToken!)}}">
                <input type="hidden" name="returnUrl" value="{{html.Encode(returnUrl)}}">
                <label>Email <input type="email" name="email" value="{{html.Encode(email)}}" autocomplete="username" required autofocus></label>
                <label>Password <input type="password" name="password" autocomplete="current-password" required></label>
                <button type="submit">Sign in</button>
              </form>
            </body>
            </html>
            """,
            "text/html; charset=utf-8");
    }
}
