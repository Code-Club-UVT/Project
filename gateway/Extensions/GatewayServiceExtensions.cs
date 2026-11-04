using System.Threading.RateLimiting;
using Gateway.Access;
using Gateway.Data;
using Gateway.Options;
using Gateway.Routing;
using Gateway.Tokens;
using Microsoft.AspNetCore.Cors.Infrastructure;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using Yarp.ReverseProxy.Configuration;
using Yarp.ReverseProxy.Transforms;

namespace Gateway.Extensions;

public static class GatewayServiceExtensions
{
    public static IServiceCollection AddGatewayOptions(this IServiceCollection services, IHostEnvironment environment)
    {
        services.AddOptions<CoreApiOptions>()
            .BindConfiguration(CoreApiOptions.SectionName)
            .ValidateDataAnnotations()
            .Validate(
                o => environment.IsDevelopment() || !o.InternalKey.StartsWith(CoreApiOptions.PlaceholderKeyPrefix, StringComparison.Ordinal),
                "CoreApi:InternalKey is still the placeholder. Set the key shared with the core API outside Development (for example with the CoreApi__InternalKey environment variable).")
            .ValidateOnStart();

        services.AddOptions<RoutingOptions>().BindConfiguration(RoutingOptions.SectionName).ValidateDataAnnotations().ValidateOnStart();
        services.AddOptions<RateLimitOptions>().BindConfiguration(RateLimitOptions.SectionName).ValidateDataAnnotations().ValidateOnStart();
        services.AddOptions<FrontendCorsOptions>().BindConfiguration(FrontendCorsOptions.SectionName).ValidateDataAnnotations().ValidateOnStart();

        return services;
    }

    /// <summary>
    /// Takes the client address from <c>X-Forwarded-For</c>, but only when the request comes from a trusted proxy (the reverse proxy
    /// in front of the gateway). Everyone else's forwarding headers are ignored. <c>Proxy:TrustedNetworks</c> lists CIDR ranges;
    /// the default is the private range Docker uses for its networks, where only the reverse proxy can reach the gateway.
    /// </summary>
    public static IServiceCollection AddGatewayForwardedHeaders(this IServiceCollection services, IConfiguration configuration)
    {
        var trusted = configuration.GetSection("Proxy:TrustedNetworks").Get<string[]>() ?? ["172.16.0.0/12"];

        services.Configure<ForwardedHeadersOptions>(options =>
        {
            options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
            options.ForwardLimit = 1;
            options.KnownIPNetworks.Clear();
            options.KnownProxies.Clear();
            foreach (var network in trusted)
            {
                options.KnownIPNetworks.Add(System.Net.IPNetwork.Parse(network));
            }
        });

        return services;
    }

    /// <summary>Read-only access to the core API's tables (projects, memberships, users).</summary>
    public static IServiceCollection AddGatewayData(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("PostgresConnection")
            ?? throw new InvalidOperationException("ConnectionStrings:PostgresConnection is not configured.");

        services.AddDbContextFactory<GatewayDbContext>(options => options
            .UseNpgsql(connectionString)
            .UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking));

        return services;
    }

    /// <summary>YARP with routes from the database, plus the access check and token exchange that run before forwarding.</summary>
    public static IServiceCollection AddGatewayProxy(this IServiceCollection services)
    {
        services.AddSingleton<DbProxyConfigProvider>();
        services.AddSingleton<IProxyConfigProvider>(sp => sp.GetRequiredService<DbProxyConfigProvider>());
        services.AddHostedService<ProjectConfigPoller>();

        services.AddReverseProxy().AddTransforms(context =>
        {
            // The gateway is the first hop: ignore any forwarding headers the client sent and set our own.
            context.AddXForwarded(ForwardedTransformActions.Set);
            context.AddRequestHeaderRemove("Forwarded");
        });

        services.TryAddSingleton(TimeProvider.System);
        services.AddMemoryCache();
        services.AddSingleton<TtlCache>();
        services.AddSingleton<IProjectAccessService, ProjectAccessService>();

        services.AddHttpClient<CoreTokenClient>((sp, client) => CoreTokenClient.Configure(client, sp.GetRequiredService<IOptions<CoreApiOptions>>().Value));
        services.AddScoped<IProjectTokenProvider, ProjectTokenProvider>();

        return services;
    }

    /// <summary>Per user (per IP when anonymous) for all projects, and a stricter per-IP limit for the core API's auth endpoints.</summary>
    public static IServiceCollection AddGatewayRateLimiting(this IServiceCollection services)
    {
        services.AddRateLimiter(_ => { });
        services.AddOptions<RateLimiterOptions>().Configure<IOptions<RateLimitOptions>>((limiter, limits) =>
        {
            limiter.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

            limiter.AddPolicy(ProxyConfigBuilder.DefaultRateLimitPolicy, context => RateLimitPartition.GetFixedWindowLimiter(
                context.User.FindFirst("sub")?.Value is { } sub ? "user:" + sub : "ip:" + ClientKey(context),
                _ => PerMinute(limits.Value.DefaultPerMinute)));

            limiter.AddPolicy(ProxyConfigBuilder.AuthRateLimitPolicy, context => RateLimitPartition.GetFixedWindowLimiter(
                "ip:" + ClientKey(context),
                _ => PerMinute(limits.Value.AuthPerMinute)));
        });

        return services;
    }

    /// <summary>Lets the single-page apps listed in <c>Cors:AllowedOrigins</c> call the gateway with cookies.</summary>
    public static IServiceCollection AddGatewayCors(this IServiceCollection services)
    {
        services.AddCors();
        services.AddOptions<CorsOptions>().Configure<IOptions<FrontendCorsOptions>>((cors, frontend) =>
            cors.AddPolicy(ProxyConfigBuilder.CorsPolicy, policy => policy
                .WithOrigins(frontend.Value.AllowedOrigins)
                .AllowAnyHeader()
                .AllowAnyMethod()
                .AllowCredentials()
                .WithExposedHeaders("X-Correlation-Id")));

        return services;
    }

    private static string ClientKey(HttpContext context) => context.Connection.RemoteIpAddress?.ToString() ?? "unknown";

    private static FixedWindowRateLimiterOptions PerMinute(int permits) => new()
    {
        PermitLimit = permits,
        Window = TimeSpan.FromMinutes(1),
        QueueLimit = 0
    };
}
