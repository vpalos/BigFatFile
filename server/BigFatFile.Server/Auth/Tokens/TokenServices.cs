using BigFatFile.Server.Auth.Users;
using Microsoft.AspNetCore.Authorization;
using OpenIddict.Server;
using OpenIddict.Server.AspNetCore;

namespace BigFatFile.Server.Auth.Tokens;

internal static class TokenServices
{
    public static IServiceCollection AddTokens(this IServiceCollection services)
    {
        // Plain HTTP is accepted only in Development, where the server runs on localhost.
        services.AddOptions<OpenIddictServerAspNetCoreOptions>().Configure<IHostEnvironment>((options, environment) =>
            options.DisableTransportSecurityRequirement = environment.IsDevelopment());

        services.AddSingleton<ServerKeys>();
        services.AddOptions<OpenIddictServerOptions>().Configure<ServerKeys>((options, keys) =>
        {
            options.SigningCredentials.Add(keys.Signing);
            options.EncryptionCredentials.Add(keys.Encryption);
        });

        services.AddOpenIddict()
            .AddCore(options => options
                .UseEntityFrameworkCore()
                .UseDbContext<AuthDbContext>()
                .ReplaceDefaultEntities<Guid>())
            .AddServer(options =>
            {
                options.SetAuthorizationEndpointUris("connect/authorize")
                    .SetTokenEndpointUris("connect/token")
                    .SetRevocationEndpointUris("connect/revoke")
                    .SetIntrospectionEndpointUris("connect/introspect")
                    .SetUserInfoEndpointUris("connect/userinfo");

                options.AllowAuthorizationCodeFlow()
                    .RequireProofKeyForCodeExchange()
                    .AllowRefreshTokenFlow()
                    .AllowClientCredentialsFlow();

                options.RegisterScopes([.. BffScopes.All]);

                // Access tokens stay plain signed JWTs, so a Replica can check them offline
                // with the public key from the JWKS endpoint.
                options.DisableAccessTokenEncryption();

                options.UseAspNetCore()
                    .EnableAuthorizationEndpointPassthrough()
                    .EnableTokenEndpointPassthrough()
                    .EnableUserInfoEndpointPassthrough()
                    .EnableStatusCodePagesIntegration();
            })
            .AddValidation(options =>
            {
                options.UseLocalServer();
                options.UseAspNetCore();
            });

        services.AddSingleton<IAuthorizationHandler, ScopeRequirementHandler>();
        return services;
    }
}
