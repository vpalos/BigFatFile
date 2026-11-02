using System.Net;
using System.Text.Json;
using BigFatFile.Contracts.Api;
using Microsoft.AspNetCore.Mvc.ApiExplorer;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.Extensions.DependencyInjection;

namespace BigFatFile.Server.Tests;

public sealed class EnumParameterTests(Origin origin) : IClassFixture<Origin>
{
    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    [Fact]
    public async Task AValueThatTheSpecListsIsAccepted()
    {
        using var client = origin.CreateClient();

        var response = await client.GetAsync("/api/v1/account/external/google", Cancellation);

        Assert.NotEqual(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Theory]
    [InlineData("Google")]
    [InlineData("GOOGLE")]
    [InlineData("0")]
    [InlineData("7")]
    [InlineData("github")]
    public async Task AValueThatTheSpecDoesNotListGetsBadRequest(string provider)
    {
        using var client = origin.CreateClient();

        var response = await client.GetAsync($"/api/v1/account/external/{provider}", Cancellation);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        using var problem = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Cancellation));
        Assert.Equal(ErrorCodes.BadRequest, problem.RootElement.GetProperty("code").GetString());
    }

    [Fact]
    public void NoPathQueryOrHeaderParameterIsAPlainEnum()
    {
        var plainEnums = origin.Factory.Services.GetRequiredService<IApiDescriptionGroupCollectionProvider>()
            .ApiDescriptionGroups.Items
            .SelectMany(group => group.Items)
            .SelectMany(endpoint => endpoint.ParameterDescriptions
                .Where(parameter => parameter.Source == BindingSource.Path || parameter.Source == BindingSource.Query || parameter.Source == BindingSource.Header)
                .Where(parameter => (Nullable.GetUnderlyingType(parameter.Type) ?? parameter.Type).IsEnum)
                .Select(parameter => $"{endpoint.HttpMethod} {endpoint.RelativePath}: {parameter.Name}"))
            .ToList();

        Assert.Empty(plainEnums);
    }
}
