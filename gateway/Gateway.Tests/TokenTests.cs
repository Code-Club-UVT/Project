using System.Net;
using System.Text;
using Gateway.Options;
using Gateway.Tokens;
using Microsoft.Extensions.Caching.Memory;
using Xunit;

namespace Gateway.Tests;

public sealed class TokenTests : IDisposable
{
    private readonly FakeTimeProvider _clock = new();
    private readonly MemoryCache _memory = new(new MemoryCacheOptions());
    private readonly Guid _user = Guid.NewGuid();
    private readonly Guid _role = Guid.NewGuid();

    public void Dispose() => _memory.Dispose();

    private static HttpResponseMessage Json(HttpStatusCode status, string json)
        => new(status) { Content = new StringContent(json, Encoding.UTF8, "application/json") };

    private string Issued(TimeSpan lifetime, string token = "project-token")
        => $"{{\"token\":\"{token}\",\"expiresAt\":\"{_clock.GetUtcNow().Add(lifetime).UtcDateTime:O}\",\"roleName\":\"Editor\"}}";

    private (IdentityTokenClient Client, StubHandler Handler) Client(Func<HttpRequestMessage, HttpResponseMessage> respond)
    {
        var handler = new StubHandler(respond);
        var http = new HttpClient(handler);
        IdentityTokenClient.Configure(http, new IdentityOptions { InternalUrl = "http://identity-api:8080/", InternalKey = new string('k', 40), TimeoutSeconds = 7 });
        return (new IdentityTokenClient(http), handler);
    }

    // ---- the client

    [Fact]
    public void The_client_is_set_up_with_the_internal_key_the_timeout_and_a_base_address_that_ends_in_a_slash()
    {
        var http = new HttpClient();

        IdentityTokenClient.Configure(http, new IdentityOptions { InternalUrl = "http://identity-api:8080//", InternalKey = new string('k', 40), TimeoutSeconds = 7 });

        Assert.Equal("http://identity-api:8080/", http.BaseAddress!.ToString());
        Assert.Equal(TimeSpan.FromSeconds(7), http.Timeout);
        Assert.Equal(new string('k', 40), http.DefaultRequestHeaders.GetValues("X-Internal-Key").Single());
    }

    [Fact]
    public async Task A_token_is_asked_for_with_a_post_of_the_user_and_the_slug()
    {
        var (client, handler) = Client(_ => Json(HttpStatusCode.OK, Issued(TimeSpan.FromMinutes(5))));

        var result = await client.IssueAsync(_user, "mock", default);

        Assert.Equal(TokenOutcome.Issued, result.Outcome);
        Assert.Equal("project-token", result.Token);
        Assert.Equal(_clock.GetUtcNow().AddMinutes(5), result.ExpiresAt);
        var call = Assert.Single(handler.Calls);
        Assert.Equal(HttpMethod.Post, call.Method);
        Assert.EndsWith("/internal/project-tokens", call.Url);
        Assert.Contains(_user.ToString(), call.Body);
        Assert.Contains("\"slug\":\"mock\"", call.Body);
    }

    [Fact]
    public async Task Forbidden_and_not_found_from_identity_are_outcomes_not_errors()
    {
        var (forbidden, _) = Client(_ => new HttpResponseMessage(HttpStatusCode.Forbidden));
        var (missing, _) = Client(_ => new HttpResponseMessage(HttpStatusCode.NotFound));

        Assert.Equal(TokenOutcome.NotMember, (await forbidden.IssueAsync(_user, "mock", default)).Outcome);
        Assert.Equal(TokenOutcome.NotFound, (await missing.IssueAsync(_user, "mock", default)).Outcome);
    }

    [Theory]
    [InlineData(HttpStatusCode.InternalServerError)]
    [InlineData(HttpStatusCode.Unauthorized)]
    [InlineData(HttpStatusCode.BadGateway)]
    public async Task Anything_else_is_an_error_so_the_gateway_fails_closed(HttpStatusCode status)
    {
        var (client, _) = Client(_ => new HttpResponseMessage(status));

        await Assert.ThrowsAsync<HttpRequestException>(() => client.IssueAsync(_user, "mock", default));
    }

    [Fact]
    public async Task An_empty_answer_is_an_error()
    {
        var (client, _) = Client(_ => Json(HttpStatusCode.OK, "null"));

        await Assert.ThrowsAsync<HttpRequestException>(() => client.IssueAsync(_user, "mock", default));
    }

    // ---- the cache in front of it

    private IProjectTokenProvider Provider(IdentityTokenClient client) => new ProjectTokenProvider(client, _memory, _clock);

    [Fact]
    public async Task The_same_user_project_and_role_ask_identity_once()
    {
        var (client, handler) = Client(_ => Json(HttpStatusCode.OK, Issued(TimeSpan.FromMinutes(5))));
        var provider = Provider(client);

        var first = await provider.GetAsync(_user, "mock", _role, default);
        var second = await provider.GetAsync(_user, "mock", _role, default);

        Assert.Same(first, second);
        Assert.Single(handler.Calls);
    }

    [Fact]
    public async Task A_new_role_gets_a_new_token_at_once_without_waiting_for_the_old_one_to_expire()
    {
        var (client, handler) = Client(_ => Json(HttpStatusCode.OK, Issued(TimeSpan.FromMinutes(5))));
        var provider = Provider(client);

        await provider.GetAsync(_user, "mock", _role, default);
        await provider.GetAsync(_user, "mock", Guid.NewGuid(), default);

        Assert.Equal(2, handler.Calls.Count);
    }

    [Fact]
    public async Task Other_users_and_other_projects_never_share_a_token()
    {
        var (client, handler) = Client(_ => Json(HttpStatusCode.OK, Issued(TimeSpan.FromMinutes(5))));
        var provider = Provider(client);

        await provider.GetAsync(_user, "mock", _role, default);
        await provider.GetAsync(Guid.NewGuid(), "mock", _role, default);
        await provider.GetAsync(_user, "other", _role, default);

        Assert.Equal(3, handler.Calls.Count);
    }

    [Fact]
    public async Task A_token_that_is_about_to_expire_is_not_kept()
    {
        var (client, handler) = Client(_ => Json(HttpStatusCode.OK, Issued(TimeSpan.Zero)));
        var provider = Provider(client);

        await provider.GetAsync(_user, "mock", _role, default);
        await provider.GetAsync(_user, "mock", _role, default);

        Assert.Equal(2, handler.Calls.Count);
    }

    [Fact]
    public async Task Refusals_are_never_cached_so_a_new_membership_works_at_once()
    {
        var (client, handler) = Client(_ => new HttpResponseMessage(HttpStatusCode.Forbidden));
        var provider = Provider(client);

        await provider.GetAsync(_user, "mock", _role, default);
        await provider.GetAsync(_user, "mock", _role, default);

        Assert.Equal(2, handler.Calls.Count);
    }

    [Fact]
    public async Task Many_requests_at_once_ask_identity_only_once()
    {
        var release = new TaskCompletionSource();
        var handler = new StubHandler(_ => Json(HttpStatusCode.OK, Issued(TimeSpan.FromMinutes(5))));
        var http = new HttpClient(new DelayHandler(handler, release.Task));
        IdentityTokenClient.Configure(http, new IdentityOptions { InternalUrl = "http://identity-api:8080", InternalKey = new string('k', 40) });
        var provider = Provider(new IdentityTokenClient(http));

        var requests = Enumerable.Range(0, 20).Select(_ => provider.GetAsync(_user, "mock", _role, default)).ToList();
        await Task.Delay(100);
        release.SetResult();
        await Task.WhenAll(requests);

        Assert.Single(handler.Calls);
    }

    private sealed class DelayHandler(HttpMessageHandler inner, Task gate) : DelegatingHandler(inner)
    {
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            await gate;
            return await base.SendAsync(request, cancellationToken);
        }
    }
}
