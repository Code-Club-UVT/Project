using Identity.Entities;
using Identity.Options;
using Identity.Services.Tokens;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using PlatformKit;

namespace Identity.Extensions;

public static class SecurityServiceCollectionExtensions
{
    /// <summary>
    /// Options, signing key, password hashing, validation of identity's own edge tokens (JWT bearer, the default scheme),
    /// the internal key scheme for the gateway's <c>/internal</c> calls and a "must be logged in" default for every endpoint.
    /// </summary>
    public static IServiceCollection AddAuth(this IServiceCollection services, IHostEnvironment environment)
    {
        services.AddOptions<JwtOptions>()
            .BindConfiguration(JwtOptions.SectionName)
            .ValidateDataAnnotations()
            .Validate(
                o => environment.IsDevelopment() || !string.IsNullOrWhiteSpace(o.PrivateKeyPem),
                "Jwt:PrivateKeyPem is not set. Provide an ECDSA P-256 private key in PEM outside Development (for example with the Jwt__PrivateKeyPem environment variable).")
            .ValidateOnStart();

        services.AddOptions<RefreshTokenOptions>().BindConfiguration(RefreshTokenOptions.SectionName).ValidateDataAnnotations().ValidateOnStart();
        services.AddOptions<AuthOptions>().BindConfiguration(AuthOptions.SectionName).ValidateDataAnnotations().ValidateOnStart();
        services.AddOptions<FrontendOptions>().BindConfiguration(FrontendOptions.SectionName).ValidateDataAnnotations().ValidateOnStart();
        services.AddOptions<SeedOptions>().BindConfiguration(SeedOptions.SectionName);

        services.AddSingleton<IPasswordHasher<User>, PasswordHasher<User>>();
        services.AddSingleton<ISigningKeyProvider, SigningKeyProvider>();

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer()
            .AddInternalKeyAuthentication(environment);

        services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
            .Configure<IOptions<JwtOptions>, ISigningKeyProvider>((bearer, jwtOptions, signingKey) =>
            {
                var jwt = jwtOptions.Value;

                bearer.MapInboundClaims = false;
                bearer.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = jwt.Issuer,
                    ValidateAudience = true,
                    ValidAudience = jwt.Audience,
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = signingKey.SecurityKey,
                    ValidAlgorithms = [SecurityAlgorithms.EcdsaSha256],
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.FromSeconds(30),
                    NameClaimType = "sub",
                    RoleClaimType = "role"
                };
            });

        // Endpoints are protected unless they opt out with [AllowAnonymous].
        services.AddAuthorizationBuilder()
            .SetFallbackPolicy(new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build());

        return services;
    }
}
