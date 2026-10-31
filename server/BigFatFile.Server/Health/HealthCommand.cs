namespace BigFatFile.Server.Health;

/// <summary>
/// <c>health</c>: asks the running server for <c>/health</c> and exits with 0 when it is
/// healthy. The container health check uses it, because the image has no shell and no curl.
/// </summary>
internal static class HealthCommand
{
    public static async Task<int> RunAsync()
    {
        var port = (Environment.GetEnvironmentVariable("ASPNETCORE_HTTP_PORTS") ?? "8080").Split(';', ',')[0];
        using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
        try
        {
            using var response = await client.GetAsync($"http://127.0.0.1:{port}/health");
            Console.WriteLine($"{(int)response.StatusCode} {await response.Content.ReadAsStringAsync()}");
            return response.IsSuccessStatusCode ? 0 : 1;
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException)
        {
            Console.WriteLine(exception.Message);
            return 1;
        }
    }
}
