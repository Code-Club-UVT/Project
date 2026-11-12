using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Logging;

namespace PlatformKit.Tests.Support;

/// <summary>A tiny local web server that publishes a JWKS, so the real key download is exercised.</summary>
public sealed class JwksServer : IAsyncDisposable
{
    private readonly WebApplication _app;

    private JwksServer(WebApplication app, string url)
    {
        _app = app;
        Url = url;
    }

    public string Url { get; }

    public static async Task<JwksServer> StartAsync(Func<string> json)
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Logging.ClearProviders();

        var app = builder.Build();
        app.MapGet("/jwks.json", () => Microsoft.AspNetCore.Http.Results.Content(json(), "application/json"));
        await app.StartAsync();

        return new JwksServer(app, app.Urls.First() + "/jwks.json");
    }

    public async ValueTask DisposeAsync()
    {
        await _app.StopAsync();
        await _app.DisposeAsync();
    }
}
