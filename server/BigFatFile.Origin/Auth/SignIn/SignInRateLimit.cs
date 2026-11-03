using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Threading.RateLimiting;
using BigFatFile.Origin.Api.Errors;
using Microsoft.AspNetCore.RateLimiting;

namespace BigFatFile.Origin.Auth.SignIn;

/// <summary>Limits how many sign-in attempts one client address makes per minute, whatever the account.</summary>
internal static class SignInRateLimit
{
    public const string Policy = "sign-in";
    public const int AttemptsPerMinute = 20;

    public static IServiceCollection AddSignInRateLimit(this IServiceCollection services) =>
        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.OnRejected = async (context, _) =>
            {
                if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
                {
                    context.HttpContext.Response.Headers.RetryAfter = Math.Ceiling(retryAfter.TotalSeconds).ToString(CultureInfo.InvariantCulture);
                }

                await Problems.Of(ApiErrors.TooManyRequests).ExecuteAsync(context.HttpContext);
            };
            options.AddPolicy(Policy, context => RateLimitPartition.GetFixedWindowLimiter(
                ClientKey(context.Connection.RemoteIpAddress),
                _ => new FixedWindowRateLimiterOptions { PermitLimit = AttemptsPerMinute, Window = TimeSpan.FromMinutes(1) }));
        });

    // One IPv6 client usually has a whole /64 block of addresses, so the block counts as one client.
    private static string ClientKey(IPAddress? address)
    {
        if (address is null)
        {
            return "";
        }

        // Kestrel reports an IPv4 client as ::ffff:a.b.c.d. The first 64 bits of every such
        // address are zero, so without this step all IPv4 clients would share one key.
        if (address.IsIPv4MappedToIPv6)
        {
            address = address.MapToIPv4();
        }

        if (address.AddressFamily != AddressFamily.InterNetworkV6)
        {
            return address.ToString();
        }

        var bytes = address.GetAddressBytes();
        Array.Clear(bytes, 8, 8);
        return new IPAddress(bytes) + "/64";
    }
}
