using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using PlatformKit.Auth;
using PlatformKit.RequestContext;
using Xunit;

namespace PlatformKit.Tests;

public class RequestContextTests
{
    private static HttpRequestContext ContextFor(HttpContext? http, string audience = "core")
    {
        var accessor = new HttpContextAccessor { HttpContext = http };
        return new HttpRequestContext(accessor, Options.Create(new PlatformAuthOptions { Audience = audience }));
    }

    private static DefaultHttpContext Authenticated(Guid userId)
    {
        var identity = new ClaimsIdentity(
            [new Claim("sub", userId.ToString()), new Claim("username", "ada"), new Claim("role", "Admin")],
            "test");
        return new DefaultHttpContext { User = new ClaimsPrincipal(identity) };
    }

    [Fact]
    public void An_authenticated_request_exposes_the_claims()
    {
        var id = Guid.NewGuid();

        var context = ContextFor(Authenticated(id), "mock");

        Assert.True(context.IsAuthenticated);
        Assert.Equal(id, context.UserId);
        Assert.Equal("ada", context.Username);
        Assert.Equal("Admin", context.Role);
        Assert.Equal("mock", context.ProjectSlug);
    }

    [Fact]
    public void An_anonymous_request_has_no_user()
    {
        var context = ContextFor(new DefaultHttpContext());

        Assert.False(context.IsAuthenticated);
        Assert.Null(context.UserId);
        Assert.Null(context.Username);
        Assert.Null(context.Role);
    }

    [Fact]
    public void A_sub_that_is_not_a_guid_gives_no_user_id()
    {
        var identity = new ClaimsIdentity([new Claim("sub", "not-a-guid")], "test");

        var context = ContextFor(new DefaultHttpContext { User = new ClaimsPrincipal(identity) });

        Assert.Null(context.UserId);
    }

    [Fact]
    public void Without_a_request_the_values_are_empty()
    {
        var context = ContextFor(null);

        Assert.False(context.IsAuthenticated);
        Assert.Equal(string.Empty, context.CorrelationId);
        Assert.Null(context.ClientIp);
    }

    [Fact]
    public void The_correlation_id_falls_back_to_the_trace_identifier()
    {
        var http = new DefaultHttpContext { TraceIdentifier = "trace-1" };

        Assert.Equal("trace-1", ContextFor(http).CorrelationId);
    }

    private static async Task<string> RunMiddlewareAsync(string? incoming)
    {
        var http = new DefaultHttpContext();
        if (incoming is not null)
        {
            http.Request.Headers["X-Correlation-Id"] = incoming;
        }

        var middleware = new RequestContextMiddleware(_ => Task.CompletedTask, NullLogger<RequestContextMiddleware>.Instance);
        await middleware.InvokeAsync(http, ContextFor(http));

        return (string)http.Items[HttpRequestContext.CorrelationIdItem]!;
    }

    [Theory]
    [InlineData("abc-123")]
    [InlineData("a.b_c:d-9")]
    [InlineData("0123456789012345678901234567890123456789012345678901234567890123")]
    public async Task A_plain_incoming_id_is_kept(string incoming)
    {
        Assert.Equal(incoming, await RunMiddlewareAsync(incoming));
    }

    [Theory]
    [InlineData("has space")]
    [InlineData("<script>alert(1)</script>")]
    [InlineData("line\nbreak")]
    [InlineData("é-accent")]
    public async Task An_unsafe_incoming_id_is_replaced(string incoming)
    {
        var id = await RunMiddlewareAsync(incoming);

        Assert.NotEqual(incoming, id);
        Assert.Matches("^[0-9a-f]{32}$", id);
    }

    [Fact]
    public async Task A_too_long_incoming_id_is_replaced()
    {
        var id = await RunMiddlewareAsync(new string('a', 65));

        Assert.Matches("^[0-9a-f]{32}$", id);
    }

    [Fact]
    public async Task A_missing_id_is_generated()
    {
        var id = await RunMiddlewareAsync(null);

        Assert.Matches("^[0-9a-f]{32}$", id);
    }
}
