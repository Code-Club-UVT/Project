using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using PlatformKit.Internal;
using PlatformKit.Tests.Support;
using Xunit;

namespace PlatformKit.Tests;

public class InternalAuthExtensionsTests
{
    private const string GoodKey = "good-key-0123456789-0123456789-abcdef-xyz";

    private static InternalAuthOptions Resolve(string environment, Dictionary<string, string?> settings)
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(settings).Build();
        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(config);
        services.AddAuthentication().AddInternalKeyAuthentication(new TestEnvironment(environment));

        using var provider = services.BuildServiceProvider();
        return provider.GetRequiredService<IOptions<InternalAuthOptions>>().Value;
    }

    [Fact]
    public void Valid_callers_are_accepted()
    {
        var options = Resolve("Production", new() { ["InternalAuth:Callers:gateway"] = GoodKey });

        Assert.Equal(GoodKey, options.Callers["gateway"]);
    }

    [Fact]
    public void No_callers_is_refused()
    {
        var ex = Assert.Throws<OptionsValidationException>(() => Resolve("Production", []));

        Assert.Contains("Callers is empty", string.Join(" ", ex.Failures));
    }

    [Fact]
    public void A_short_key_is_refused_everywhere()
    {
        Assert.Throws<OptionsValidationException>(() =>
            Resolve("Development", new() { ["InternalAuth:Callers:gateway"] = "too-short" }));
    }

    [Fact]
    public void A_placeholder_key_is_refused_outside_development()
    {
        var placeholder = "CHANGE-ME-" + new string('x', 40);

        var ex = Assert.Throws<OptionsValidationException>(() =>
            Resolve("Production", new() { ["InternalAuth:Callers:gateway"] = placeholder }));

        Assert.Contains("placeholder", string.Join(" ", ex.Failures));
    }

    [Fact]
    public void A_placeholder_key_is_allowed_in_development()
    {
        var placeholder = "CHANGE-ME-" + new string('x', 40);

        var options = Resolve("Development", new() { ["InternalAuth:Callers:gateway"] = placeholder });

        Assert.Equal(placeholder, options.Callers["gateway"]);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("CHANGE-ME-placeholder")]
    public void An_internal_client_refuses_a_missing_or_placeholder_key_in_production(string key)
    {
        var services = new ServiceCollection();

        Assert.Throws<InvalidOperationException>(() =>
            services.AddInternalClient("core", new Uri("http://core-api:8080"), key, TimeSpan.FromSeconds(5), new TestEnvironment("Production")));
    }

    [Fact]
    public void An_internal_client_with_a_real_key_is_registered()
    {
        var services = new ServiceCollection();

        var builder = services.AddInternalClient("core", new Uri("http://core-api:8080"), GoodKey, TimeSpan.FromSeconds(5), new TestEnvironment("Production"));

        Assert.NotNull(builder);
    }
}
