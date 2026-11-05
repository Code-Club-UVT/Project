using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using PlatformKit.Internal;
using PlatformKit.RequestContext;

namespace PlatformKit;

public static class InternalAuthExtensions
{
    /// <summary>
    /// Adds the <see cref="InternalAuth.SchemeName"/> scheme: callers listed in <c>InternalAuth:Callers</c> authenticate with
    /// their key in <c>X-Internal-Key</c>. Use it only on <c>/internal/...</c> endpoints, which the gateway never routes.
    /// Outside Development every key must be at least 32 characters and not a <c>CHANGE-ME</c> placeholder.
    /// Call after <c>AddAuthentication</c> / <c>AddPlatformAuthentication</c> (the default scheme stays as it is).
    /// </summary>
    public static AuthenticationBuilder AddInternalKeyAuthentication(this AuthenticationBuilder builder, IHostEnvironment environment)
    {
        builder.Services.AddOptions<InternalAuthOptions>()
            .BindConfiguration(InternalAuthOptions.SectionName)
            .Validate(o => o.Callers.Count > 0, "InternalAuth:Callers is empty: list the services that may call /internal endpoints.")
            .Validate(
                o => o.Callers.Values.All(key => key.Length >= InternalAuthOptions.MinKeyLength),
                $"Every InternalAuth:Callers key must be at least {InternalAuthOptions.MinKeyLength} characters.")
            .Validate(
                o => environment.IsDevelopment() || o.Callers.Values.All(key => !key.StartsWith(InternalAuthOptions.PlaceholderKeyPrefix, StringComparison.Ordinal)),
                "An InternalAuth:Callers key is still the CHANGE-ME placeholder. Set real keys outside Development.")
            .ValidateOnStart();

        return builder.AddScheme<AuthenticationSchemeOptions, InternalKeyAuthenticationHandler>(InternalAuth.SchemeName, null);
    }

    /// <summary>
    /// An HttpClient for calling another service's <c>/internal/...</c> endpoints: sends the key in <c>X-Internal-Key</c>
    /// and the current correlation id. Outside Development the key must not be a <c>CHANGE-ME</c> placeholder.
    /// </summary>
    public static IHttpClientBuilder AddInternalClient(this IServiceCollection services, string name, Uri baseAddress, string key, TimeSpan timeout, IHostEnvironment environment)
    {
        if (string.IsNullOrWhiteSpace(key) || (!environment.IsDevelopment() && key.StartsWith(InternalAuthOptions.PlaceholderKeyPrefix, StringComparison.Ordinal)))
        {
            throw new InvalidOperationException($"The internal key for '{name}' is missing or still the CHANGE-ME placeholder.");
        }

        return services.AddHttpClient(name, client =>
            {
                client.BaseAddress = baseAddress;
                client.Timeout = timeout;
            })
            .AddHttpMessageHandler(() => new InternalKeyHandler(key))
            .AddCorrelationIdPropagation();
    }
}
