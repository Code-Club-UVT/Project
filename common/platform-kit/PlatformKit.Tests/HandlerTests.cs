using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using PlatformKit.Internal;
using PlatformKit.RequestContext;
using PlatformKit.Tests.Support;
using Xunit;

namespace PlatformKit.Tests;

public class HandlerTests
{
    private sealed class FakeRequestContext(string correlationId) : IRequestContext
    {
        public bool IsAuthenticated => false;
        public Guid? UserId => null;
        public string? Username => null;
        public string? Role => null;
        public string ProjectSlug => "core";
        public string CorrelationId { get; } = correlationId;
        public string? ClientIp => null;
    }

    private static HttpResponseMessage Ok(HttpRequestMessage _) => new(System.Net.HttpStatusCode.OK);

    [Fact]
    public async Task The_internal_key_is_added_to_every_call()
    {
        var stub = new StubHandler(Ok);
        using var invoker = new HttpMessageInvoker(new InternalKeyHandler("secret-key") { InnerHandler = stub });

        await invoker.SendAsync(new HttpRequestMessage(HttpMethod.Post, "http://core/internal/users"), CancellationToken.None);

        Assert.Equal("secret-key", Assert.Single(stub.Requests).Headers.GetValues(InternalAuth.HeaderName).Single());
    }

    [Fact]
    public async Task A_key_set_by_the_caller_is_replaced()
    {
        var stub = new StubHandler(Ok);
        using var invoker = new HttpMessageInvoker(new InternalKeyHandler("secret-key") { InnerHandler = stub });
        var request = new HttpRequestMessage(HttpMethod.Post, "http://core/internal/users");
        request.Headers.TryAddWithoutValidation(InternalAuth.HeaderName, "something-else");

        await invoker.SendAsync(request, CancellationToken.None);

        Assert.Equal("secret-key", Assert.Single(stub.Requests).Headers.GetValues(InternalAuth.HeaderName).Single());
    }

    private static CorrelationIdHandler HandlerFor(string? correlationId, StubHandler stub)
    {
        var accessor = new HttpContextAccessor();
        if (correlationId is not null)
        {
            var services = new ServiceCollection();
            services.AddSingleton<IRequestContext>(new FakeRequestContext(correlationId));
            accessor.HttpContext = new DefaultHttpContext { RequestServices = services.BuildServiceProvider() };
        }

        return new CorrelationIdHandler(accessor) { InnerHandler = stub };
    }

    [Fact]
    public async Task The_correlation_id_of_the_current_request_is_sent_on()
    {
        var stub = new StubHandler(Ok);
        using var invoker = new HttpMessageInvoker(HandlerFor("abc-123", stub));

        await invoker.SendAsync(new HttpRequestMessage(HttpMethod.Get, "http://core/x"), CancellationToken.None);

        Assert.Equal("abc-123", Assert.Single(stub.Requests).Headers.GetValues("X-Correlation-Id").Single());
    }

    [Fact]
    public async Task An_id_already_on_the_call_is_kept()
    {
        var stub = new StubHandler(Ok);
        using var invoker = new HttpMessageInvoker(HandlerFor("abc-123", stub));
        var request = new HttpRequestMessage(HttpMethod.Get, "http://core/x");
        request.Headers.TryAddWithoutValidation("X-Correlation-Id", "explicit");

        await invoker.SendAsync(request, CancellationToken.None);

        Assert.Equal("explicit", Assert.Single(stub.Requests).Headers.GetValues("X-Correlation-Id").Single());
    }

    [Fact]
    public async Task Outside_a_request_nothing_is_added()
    {
        var stub = new StubHandler(Ok);
        using var invoker = new HttpMessageInvoker(HandlerFor(null, stub));

        await invoker.SendAsync(new HttpRequestMessage(HttpMethod.Get, "http://core/x"), CancellationToken.None);

        Assert.False(Assert.Single(stub.Requests).Headers.Contains("X-Correlation-Id"));
    }
}
