using GatewayAuth.Authentication;
using GatewayAuth.Options;
using GatewayAuth.RequestContext;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace GatewayAuth;

public static class GatewayAuthExtensions
{
    /// <summary>
    /// Validates project tokens: ES256 signature (keys come from the JWKS URL), issuer, this API's slug as audience and lifetime.
    /// Every endpoint requires a valid token unless it says <c>[AllowAnonymous]</c>. The user's role in the project is the
    /// <c>role</c> claim, so <c>[Authorize(Roles = "Editor")]</c> works.
    /// </summary>
    public static IServiceCollection AddGatewayAuthentication(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<GatewayAuthOptions>()
            .Bind(configuration.GetSection(GatewayAuthOptions.SectionName))
            .ValidateDataAnnotations()
            .Validate(IsValidJwksUrl, "GatewayAuth:JwksUrl must be an absolute https URL (or http with GatewayAuth:AllowInsecureJwksUrl=true).")
            .ValidateOnStart();

        // One long-lived client; its connections are recycled so a changed address of the core API is picked up.
        services.AddSingleton(sp => new JwksKeyStore(
            new HttpClient(new SocketsHttpHandler { PooledConnectionLifetime = TimeSpan.FromMinutes(5) }) { Timeout = TimeSpan.FromSeconds(5) },
            sp.GetRequiredService<IOptions<GatewayAuthOptions>>(),
            sp.GetRequiredService<TimeProvider>(),
            sp.GetRequiredService<ILogger<JwksKeyStore>>()));
        services.TryAddSingleton(TimeProvider.System);

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer();
        services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
            .Configure<IOptions<GatewayAuthOptions>, JwksKeyStore>((bearer, gatewayOptions, keyStore) =>
            {
                var gateway = gatewayOptions.Value;

                bearer.MapInboundClaims = false;
                bearer.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = gateway.Issuer,
                    ValidateAudience = true,
                    ValidAudience = gateway.Audience,
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKeyResolver = (_, _, kid, _) => keyStore.GetKeys(kid),
                    ValidAlgorithms = [SecurityAlgorithms.EcdsaSha256],
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.FromSeconds(30),
                    NameClaimType = "sub",
                    RoleClaimType = "role"
                };
            });

        services.AddAuthorizationBuilder()
            .SetFallbackPolicy(new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build());

        return services;
    }

    /// <summary>Registers <see cref="IRequestContext"/> (scoped). Also needs <see cref="UseRequestContext"/> in the pipeline.</summary>
    public static IServiceCollection AddRequestContext(this IServiceCollection services)
    {
        services.AddHttpContextAccessor();
        services.AddScoped<IRequestContext, HttpRequestContext>();

        return services;
    }

    /// <summary>Call after <c>UseAuthentication()</c>. Settles the correlation id and adds it, the user and the project to every log entry of the request.</summary>
    public static IApplicationBuilder UseRequestContext(this IApplicationBuilder app)
    {
        return app.UseMiddleware<RequestContextMiddleware>();
    }

    /// <summary>Makes an <see cref="HttpClient"/> send the current correlation id (<c>X-Correlation-Id</c>) on its calls.</summary>
    public static IHttpClientBuilder AddCorrelationIdPropagation(this IHttpClientBuilder builder)
    {
        builder.Services.AddHttpContextAccessor();
        builder.Services.AddTransient<CorrelationIdHandler>();

        return builder.AddHttpMessageHandler<CorrelationIdHandler>();
    }

    /// <summary>Adds health checks; chain further checks on the returned builder.</summary>
    public static IHealthChecksBuilder AddGatewayHealth(this IServiceCollection services)
    {
        return services.AddHealthChecks();
    }

    /// <summary>Maps an anonymous <c>/health</c> endpoint, the same in every API, for the gateway's probes.</summary>
    public static IEndpointConventionBuilder MapGatewayHealth(this IEndpointRouteBuilder endpoints)
    {
        return endpoints.MapHealthChecks("/health").AllowAnonymous();
    }

    private static bool IsValidJwksUrl(GatewayAuthOptions options)
    {
        return Uri.TryCreate(options.JwksUrl, UriKind.Absolute, out var uri)
            && (uri.Scheme == Uri.UriSchemeHttps || (uri.Scheme == Uri.UriSchemeHttp && options.AllowInsecureJwksUrl));
    }
}
