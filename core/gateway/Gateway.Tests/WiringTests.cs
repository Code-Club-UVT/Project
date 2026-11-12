using Gateway.Extensions;
using Gateway.Options;
using Gateway.Routing;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Cors.Infrastructure;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Xunit;
using Yarp.ReverseProxy.Configuration;

namespace Gateway.Tests;

public class WiringTests
{
    private sealed class TestEnvironment(string name) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = name;
        public string ApplicationName { get; set; } = "tests";
        public string ContentRootPath { get; set; } = string.Empty;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }

    private static IConfiguration Config(Dictionary<string, string?> values) => new ConfigurationBuilder().AddInMemoryCollection(values).Build();

    // ---- options

    private static IdentityOptions ResolveIdentity(string environment, string key, string url = "http://identity-api:8080")
    {
        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(Config(new() { ["Identity:InternalUrl"] = url, ["Identity:InternalKey"] = key }));
        services.AddGatewayOptions(new TestEnvironment(environment));
        using var provider = services.BuildServiceProvider();
        return provider.GetRequiredService<IOptions<IdentityOptions>>().Value;
    }

    [Fact]
    public void A_real_internal_key_is_accepted()
    {
        Assert.Equal(new string('k', 40), ResolveIdentity("Production", new string('k', 40)).InternalKey);
    }

    [Fact]
    public void A_placeholder_internal_key_is_refused_outside_development_and_allowed_in_it()
    {
        var placeholder = "CHANGE-ME-" + new string('x', 40);

        Assert.Throws<OptionsValidationException>(() => ResolveIdentity("Production", placeholder));
        Assert.Equal(placeholder, ResolveIdentity("Development", placeholder).InternalKey);
    }

    [Theory]
    [InlineData("short")]
    [InlineData("")]
    public void A_short_or_missing_internal_key_is_refused_everywhere(string key)
    {
        Assert.Throws<OptionsValidationException>(() => ResolveIdentity("Development", key));
    }

    [Fact]
    public void The_identity_address_must_be_a_url()
    {
        Assert.Throws<OptionsValidationException>(() => ResolveIdentity("Production", new string('k', 40), "not a url"));
    }

    [Fact]
    public void The_defaults_match_what_the_docs_say()
    {
        var routing = new RoutingOptions();
        var limits = new RateLimitOptions();

        Assert.Equal((30, 30, 300), (routing.PollSeconds, routing.AccessCacheSeconds, routing.StaleGraceSeconds));
        Assert.Equal((10, 300), (limits.AuthPerMinute, limits.DefaultPerMinute));
    }

    // ---- forwarded headers

    private static ForwardedHeadersOptions Forwarded(Dictionary<string, string?> values)
    {
        var services = new ServiceCollection();
        services.AddGatewayForwardedHeaders(Config(values));
        using var provider = services.BuildServiceProvider();
        return provider.GetRequiredService<IOptions<ForwardedHeadersOptions>>().Value;
    }

    [Fact]
    public void By_default_only_the_private_docker_range_may_set_the_client_address_and_only_one_hop_is_read()
    {
        var options = Forwarded([]);

        var network = Assert.Single(options.KnownIPNetworks);
        Assert.Equal(System.Net.IPNetwork.Parse("172.16.0.0/12"), network);
        Assert.Empty(options.KnownProxies);
        Assert.Equal(1, options.ForwardLimit);
        Assert.Equal(ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto, options.ForwardedHeaders);
    }

    [Fact]
    public void The_trusted_networks_can_be_narrowed_by_configuration()
    {
        var options = Forwarded(new() { ["Proxy:TrustedNetworks:0"] = "10.1.2.0/24", ["Proxy:TrustedNetworks:1"] = "192.168.5.0/24" });

        Assert.Equal(
            [System.Net.IPNetwork.Parse("10.1.2.0/24"), System.Net.IPNetwork.Parse("192.168.5.0/24")],
            options.KnownIPNetworks);
    }

    // ---- cors

    private static async Task<Microsoft.AspNetCore.Cors.Infrastructure.CorsPolicy?> PolicyAsync(params string[] origins)
    {
        var values = origins.Select((o, i) => ($"Cors:AllowedOrigins:{i}", (string?)o)).ToDictionary(x => x.Item1, x => x.Item2);
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IConfiguration>(Config(values));
        services.AddOptions<FrontendCorsOptions>().BindConfiguration(FrontendCorsOptions.SectionName);
        services.AddGatewayCors();
        using var provider = services.BuildServiceProvider();

        return await provider.GetRequiredService<ICorsPolicyProvider>().GetPolicyAsync(new DefaultHttpContext(), "frontend");
    }

    [Fact]
    public async Task Only_the_listed_origins_may_call_the_gateway_with_credentials()
    {
        var policy = await PolicyAsync("https://app.example.org");

        Assert.NotNull(policy);
        Assert.True(policy.SupportsCredentials);
        Assert.True(policy.IsOriginAllowed("https://app.example.org"));
        Assert.False(policy.IsOriginAllowed("https://evil.example.com"));
        Assert.False(policy.IsOriginAllowed("https://app.example.org.evil.com"));
        Assert.False(policy.AllowAnyOrigin);
        Assert.Contains("X-Correlation-Id", policy.ExposedHeaders);
    }

    // ---- readiness and the route holder

    [Fact]
    public async Task The_gateway_is_not_ready_until_the_routes_were_read_once()
    {
        var provider = new DbProxyConfigProvider();
        var check = new RoutesLoadedHealthCheck(provider);

        Assert.Equal(HealthStatus.Unhealthy, (await check.CheckHealthAsync(new HealthCheckContext())).Status);

        provider.Update([], []);

        Assert.Equal(HealthStatus.Healthy, (await check.CheckHealthAsync(new HealthCheckContext())).Status);
    }

    [Fact]
    public void Replacing_the_routes_signals_the_old_configuration_and_serves_the_new_one()
    {
        var provider = new DbProxyConfigProvider();
        var old = provider.GetConfig();
        Assert.Empty(old.Routes);
        Assert.False(old.ChangeToken.HasChanged);
        var route = new RouteConfig { RouteId = "r", ClusterId = "c", Match = new RouteMatch { Path = "/x" } };

        provider.Update([route], []);

        Assert.True(old.ChangeToken.HasChanged);
        Assert.Equal("r", Assert.Single(provider.GetConfig().Routes).RouteId);
    }
}
