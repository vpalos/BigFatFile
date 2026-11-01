using System.Net;
using System.Reflection;
using System.Text.Json;
using BigFatFile.Contracts.Api;
using BigFatFile.Server.Api;
using Microsoft.AspNetCore.Http;

namespace BigFatFile.Server.Tests;

public sealed class ErrorTests(Origin origin) : IClassFixture<Origin>
{
    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    [Fact]
    public async Task AnErrorThatTheFrameworkWritesGetsACode()
    {
        using var client = origin.CreateClient();

        var response = await client.GetAsync("/api/v1/no-such-endpoint", Cancellation);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        using var problem = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Cancellation));
        Assert.Equal(ErrorCodes.NotFound, problem.RootElement.GetProperty("code").GetString());
        Assert.Equal(ApiErrors.NotFound.Type, problem.RootElement.GetProperty("type").GetString());
        Assert.Equal(ApiErrors.NotFound.Title, problem.RootElement.GetProperty("title").GetString());
        Assert.False(string.IsNullOrEmpty(problem.RootElement.GetProperty("traceId").GetString()));
    }

    [Fact]
    public async Task ARequestWithoutATokenGetsAuthenticationRequired()
    {
        using var client = origin.CreateClient();

        var response = await client.GetAsync("/api/v1/me", Cancellation);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        using var problem = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Cancellation));
        Assert.Equal(ErrorCodes.AuthenticationRequired, problem.RootElement.GetProperty("code").GetString());
    }

    [Fact]
    public async Task FieldErrorsKeepTheirFieldsAndGetTheirCode()
    {
        var context = new DefaultHttpContext { RequestServices = origin.Factory.Services };
        context.Response.Body = new MemoryStream();

        await Problems.InvalidFields(new Dictionary<string, string[]> { ["password"] = ["Too short."] }).ExecuteAsync(context);

        Assert.Equal(StatusCodes.Status400BadRequest, context.Response.StatusCode);
        context.Response.Body.Position = 0;
        using var problem = await JsonDocument.ParseAsync(context.Response.Body, cancellationToken: Cancellation);
        Assert.Equal(ErrorCodes.InvalidFields, problem.RootElement.GetProperty("code").GetString());
        Assert.Equal("Too short.", problem.RootElement.GetProperty("errors").GetProperty("password")[0].GetString());
        Assert.True(problem.RootElement.TryGetProperty("traceId", out _));
    }

    [Fact]
    public void EveryCodeHasExactlyOneDefinition()
    {
        var codes = typeof(ErrorCodes).GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(field => field.IsLiteral)
            .Select(field => (string)field.GetRawConstantValue()!);
        var defined = ApiErrors.All.Select(error => error.Code).ToList();

        Assert.Equal(defined.Count, defined.Distinct().Count());
        Assert.Equal(codes.Order(), defined.Order());
    }
}
