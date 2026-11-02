using System.Text;
using System.Threading.RateLimiting;
using CoreApi.Entities;
using CoreApi.Options;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace CoreApi.Extensions;

public static class SecurityServiceCollectionExtensions
{
    public const string AuthRateLimitPolicy = "auth";

    /// <summary>Options, password hashing, JWT bearer authentication and a "must be logged in" default for every endpoint.</summary>
    public static IServiceCollection AddAuth(this IServiceCollection services, IHostEnvironment environment)
    {
        services.AddOptions<JwtOptions>()
            .BindConfiguration(JwtOptions.SectionName)
            .ValidateDataAnnotations()
            .Validate(
                o => environment.IsDevelopment() || !o.SigningKey.StartsWith(JwtOptions.PlaceholderKeyPrefix, StringComparison.Ordinal),
                "Jwt:SigningKey is still the placeholder. Set a real key outside Development (for example with the Jwt__SigningKey environment variable).")
            .ValidateOnStart();

        services.AddOptions<RefreshTokenOptions>().BindConfiguration(RefreshTokenOptions.SectionName).ValidateDataAnnotations().ValidateOnStart();
        services.AddOptions<AuthOptions>().BindConfiguration(AuthOptions.SectionName).ValidateDataAnnotations().ValidateOnStart();
        services.AddOptions<FrontendOptions>().BindConfiguration(FrontendOptions.SectionName).ValidateDataAnnotations().ValidateOnStart();
        services.AddOptions<SeedOptions>().BindConfiguration(SeedOptions.SectionName);

        services.AddSingleton<IPasswordHasher<User>, PasswordHasher<User>>();

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer();
        services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
            .Configure<IOptions<JwtOptions>>((bearer, jwtOptions) =>
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
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.SigningKey)),
                    ValidAlgorithms = [SecurityAlgorithms.HmacSha256],
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

    public static IServiceCollection AddRateLimiting(this IServiceCollection services)
    {
        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.AddPolicy(AuthRateLimitPolicy, httpContext => RateLimitPartition.GetFixedWindowLimiter(
                httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = 10,
                    Window = TimeSpan.FromMinutes(1),
                    QueueLimit = 0
                }));
        });

        return services;
    }

    /// <summary>Allows the SPA origin (taken from Frontend:BaseUrl) to call the API with cookies.</summary>
    public static IServiceCollection AddFrontendCors(this IServiceCollection services, IConfiguration configuration)
    {
        var baseUrl = configuration[$"{FrontendOptions.SectionName}:BaseUrl"]
            ?? throw new InvalidOperationException("Frontend:BaseUrl is not configured.");
        var origin = new Uri(baseUrl).GetLeftPart(UriPartial.Authority);

        services.AddCors(options => options.AddDefaultPolicy(policy => policy
            .WithOrigins(origin)
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials()));

        return services;
    }
}
