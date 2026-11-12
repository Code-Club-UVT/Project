using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using PlatformKit.Tests.Support;
using PlatformKit.Web;
using Xunit;

namespace PlatformKit.Tests;

public class GlobalExceptionHandlerTests
{
    private static async Task<(bool Handled, int Status, JsonElement Body)> RunAsync(string environment, Exception exception, bool aborted = false)
    {
        using var cts = new CancellationTokenSource();
        var http = new DefaultHttpContext { RequestAborted = cts.Token };
        http.Response.Body = new MemoryStream();
        if (aborted)
        {
            await cts.CancelAsync();
        }

        var handler = new GlobalExceptionHandler(NullLogger<GlobalExceptionHandler>.Instance, new TestEnvironment(environment));
        var handled = await handler.TryHandleAsync(http, exception, CancellationToken.None);

        http.Response.Body.Position = 0;
        var text = await new StreamReader(http.Response.Body).ReadToEndAsync();
        var body = text.Length == 0 ? default : JsonDocument.Parse(text).RootElement.Clone();
        return (handled, http.Response.StatusCode, body);
    }

    [Fact]
    public async Task An_unhandled_exception_is_a_500_problem_without_the_message_in_production()
    {
        var (handled, status, body) = await RunAsync("Production", new InvalidOperationException("secret detail"));

        Assert.True(handled);
        Assert.Equal(500, status);
        Assert.Equal("An unexpected error occurred.", body.GetProperty("title").GetString());
        Assert.False(body.TryGetProperty("detail", out var detail) && detail.ValueKind != JsonValueKind.Null);
    }

    [Fact]
    public async Task The_message_is_shown_in_development()
    {
        var (_, status, body) = await RunAsync("Development", new InvalidOperationException("secret detail"));

        Assert.Equal(500, status);
        Assert.Equal("secret detail", body.GetProperty("detail").GetString());
    }

    [Fact]
    public async Task A_client_that_went_away_gets_no_response_body()
    {
        var (handled, _, body) = await RunAsync("Production", new OperationCanceledException(), aborted: true);

        Assert.True(handled);
        Assert.Equal(default, body);
    }

    [Fact]
    public async Task A_cancellation_that_is_not_the_clients_is_still_an_error()
    {
        var (handled, status, _) = await RunAsync("Production", new OperationCanceledException(), aborted: false);

        Assert.True(handled);
        Assert.Equal(500, status);
    }
}
