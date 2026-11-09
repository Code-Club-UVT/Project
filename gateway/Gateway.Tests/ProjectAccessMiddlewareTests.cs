using System.Security.Claims;
using System.Text.Json;
using Gateway.Access;
using Gateway.Pipeline;
using Gateway.Routing;
using Gateway.Tokens;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Net.Http.Headers;
using PlatformKit.RequestContext;
using Xunit;
using Yarp.ReverseProxy.Configuration;
using Yarp.ReverseProxy.Forwarder;
using Yarp.ReverseProxy.Model;

namespace Gateway.Tests;

public class ProjectAccessMiddlewareTests
{
    private sealed class FakeAccess : IProjectAccessService
    {
        public AccessDecision Decision { get; set; } = new(true, true, Guid.NewGuid());
        public Exception? Failure { get; set; }
        public List<(Guid UserId, Guid ProjectId)> Asked { get; } = [];

        public Task<AccessDecision> GetAsync(Guid userId, Guid projectId, CancellationToken cancellationToken)
        {
            Asked.Add((userId, projectId));
            return Failure is null ? Task.FromResult(Decision) : throw Failure;
        }
    }

    private sealed class FakeTokens : IProjectTokenProvider
    {
        public TokenResult Result { get; set; } = new(TokenOutcome.Issued, "project-token", DateTimeOffset.UtcNow.AddMinutes(5));
        public Exception? Failure { get; set; }
        public List<(Guid UserId, string Slug, Guid RoleId)> Asked { get; } = [];

        public Task<TokenResult> GetAsync(Guid userId, string slug, Guid roleId, CancellationToken cancellationToken)
        {
            Asked.Add((userId, slug, roleId));
            return Failure is null ? Task.FromResult(Result) : throw Failure;
        }
    }

    private sealed class FakeRequestContext : IRequestContext
    {
        public bool IsAuthenticated => false;
        public Guid? UserId => null;
        public string? Username => null;
        public string? Role => null;
        public string ProjectSlug => "gateway";
        public string CorrelationId => "corr-123";
        public string? ClientIp => null;
    }

    private readonly FakeAccess _access = new();
    private readonly FakeTokens _tokens = new();
    private readonly Guid _projectId = Guid.NewGuid();
    private readonly Guid _userId = Guid.NewGuid();
    private bool _nextCalled;

    private static readonly IServiceProvider Services = new ServiceCollection().AddLogging().BuildServiceProvider();

    private Dictionary<string, string> ApiRoute(string mode = "Exchange") => new()
    {
        ["Slug"] = "mock",
        ["ProjectId"] = _projectId.ToString(),
        ["TokenMode"] = mode
    };

    private Dictionary<string, string> FrontendRoute() => new()
    {
        ["Slug"] = "mock",
        ["ProjectId"] = _projectId.ToString(),
        ["RouteKind"] = "frontend"
    };

    private DefaultHttpContext Context(Dictionary<string, string>? metadata, bool authenticated = true, string? sub = null)
    {
        var context = new DefaultHttpContext { RequestServices = Services };
        context.Response.Body = new MemoryStream();

        var route = new RouteModel(new RouteConfig { RouteId = "route", Metadata = metadata }, null, HttpTransformer.Empty);
        context.Features.Set<IReverseProxyFeature>(new ReverseProxyFeature { Route = route });

        if (authenticated)
        {
            context.User = new ClaimsPrincipal(new ClaimsIdentity([new Claim("sub", sub ?? _userId.ToString())], "test"));
        }

        return context;
    }

    private Task Run(HttpContext context)
    {
        var middleware = new ProjectAccessMiddleware(_ =>
        {
            _nextCalled = true;
            return Task.CompletedTask;
        }, NullLogger<ProjectAccessMiddleware>.Instance);

        return middleware.InvokeAsync(context, _access, _tokens, new FakeRequestContext());
    }

    private static string? ProblemCode(HttpContext context)
    {
        context.Response.Body.Position = 0;
        using var document = JsonDocument.Parse(context.Response.Body);
        return document.RootElement.TryGetProperty("code", out var code) ? code.GetString() : null;
    }

    private static void AddForgedHeaders(HttpContext context)
    {
        context.Request.Headers["X-User-Id"] = "11111111-1111-1111-1111-111111111111";
        context.Request.Headers["x-user-role"] = "Admin";
        context.Request.Headers["X-Internal-Key"] = "stolen-key";
    }

    private static void AssertNoForgedHeaders(HttpContext context)
    {
        Assert.False(context.Request.Headers.ContainsKey("X-User-Id"));
        Assert.False(context.Request.Headers.ContainsKey("X-User-Role"));
        Assert.False(context.Request.Headers.ContainsKey("X-Internal-Key"));
    }

    // ---- a project's static frontend

    [Fact]
    public async Task A_frontend_needs_no_token_and_no_membership_check()
    {
        var context = Context(FrontendRoute(), authenticated: false);

        await Run(context);

        Assert.True(_nextCalled);
        Assert.Empty(_access.Asked);
        Assert.Empty(_tokens.Asked);
    }

    [Fact]
    public async Task A_frontend_gets_nothing_that_identifies_the_user_or_the_platform()
    {
        var context = Context(FrontendRoute());
        AddForgedHeaders(context);
        context.Request.Headers.Authorization = "Bearer edge-token";
        context.Request.Headers.Cookie = "refreshToken=secret";

        await Run(context);

        AssertNoForgedHeaders(context);
        Assert.False(context.Request.Headers.ContainsKey(HeaderNames.Authorization));
        Assert.False(context.Request.Headers.ContainsKey(HeaderNames.Cookie));
        Assert.Equal("corr-123", context.Request.Headers["X-Correlation-Id"]);
    }

    // ---- an API route with project tokens

    [Fact]
    public async Task A_route_without_project_metadata_is_a_503_and_nothing_is_forwarded()
    {
        var context = Context(metadata: null);

        await Run(context);

        Assert.False(_nextCalled);
        Assert.Equal(503, context.Response.StatusCode);
        Assert.Equal("Gateway.Misconfigured", ProblemCode(context));
    }

    [Fact]
    public async Task A_project_id_that_is_not_a_guid_is_a_503()
    {
        var metadata = ApiRoute();
        metadata["ProjectId"] = "nope";
        var context = Context(metadata);

        await Run(context);

        Assert.Equal(503, context.Response.StatusCode);
        Assert.False(_nextCalled);
    }

    [Fact]
    public async Task Without_a_valid_edge_token_the_answer_is_401()
    {
        var context = Context(ApiRoute(), authenticated: false);

        await Run(context);

        Assert.False(_nextCalled);
        Assert.Equal(401, context.Response.StatusCode);
        Assert.Equal("Auth.InvalidToken", ProblemCode(context));
        Assert.Empty(_access.Asked);
    }

    [Fact]
    public async Task A_token_without_a_valid_subject_is_401()
    {
        var context = Context(ApiRoute(), sub: "not-a-guid");

        await Run(context);

        Assert.Equal(401, context.Response.StatusCode);
        Assert.False(_nextCalled);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    public async Task An_unknown_or_disabled_account_is_401(bool exists, bool active)
    {
        _access.Decision = new AccessDecision(exists, active, Guid.NewGuid());
        var context = Context(ApiRoute());

        await Run(context);

        Assert.Equal(401, context.Response.StatusCode);
        Assert.Equal("Auth.AccountUnavailable", ProblemCode(context));
        Assert.False(_nextCalled);
        Assert.Empty(_tokens.Asked);
    }

    [Fact]
    public async Task A_user_who_is_not_a_member_gets_403_and_no_token_is_asked_for()
    {
        _access.Decision = new AccessDecision(true, true, null);
        var context = Context(ApiRoute());

        await Run(context);

        Assert.Equal(403, context.Response.StatusCode);
        Assert.Equal("Project.NotMember", ProblemCode(context));
        Assert.False(_nextCalled);
        Assert.Empty(_tokens.Asked);
    }

    [Theory]
    [InlineData(TokenOutcome.NotMember, 403, "Project.NotMember")]
    [InlineData(TokenOutcome.NotFound, 404, "Project.NotFound")]
    public async Task What_identity_refuses_is_passed_on(TokenOutcome outcome, int status, string code)
    {
        _tokens.Result = new TokenResult(outcome);
        var context = Context(ApiRoute());

        await Run(context);

        Assert.Equal(status, context.Response.StatusCode);
        Assert.Equal(code, ProblemCode(context));
        Assert.False(_nextCalled);
    }

    [Fact]
    public async Task A_member_is_forwarded_with_a_project_token_instead_of_the_edge_token()
    {
        var role = Guid.NewGuid();
        _access.Decision = new AccessDecision(true, true, role);
        var context = Context(ApiRoute());
        context.Request.Headers.Authorization = "Bearer edge-token";

        await Run(context);

        Assert.True(_nextCalled);
        Assert.Equal("Bearer project-token", context.Request.Headers.Authorization.ToString());
        Assert.Equal([(_userId, _projectId)], _access.Asked);
        Assert.Equal([(_userId, "mock", role)], _tokens.Asked);
        Assert.Equal("corr-123", context.Request.Headers["X-Correlation-Id"]);
    }

    [Fact]
    public async Task Headers_that_pretend_to_be_someone_else_never_reach_the_api()
    {
        var context = Context(ApiRoute());
        AddForgedHeaders(context);

        await Run(context);

        Assert.True(_nextCalled);
        AssertNoForgedHeaders(context);
    }

    [Fact]
    public async Task When_access_cannot_be_checked_the_request_is_refused_not_forwarded()
    {
        _access.Failure = new InvalidOperationException("database down");
        var context = Context(ApiRoute());

        await Run(context);

        Assert.False(_nextCalled);
        Assert.Equal(503, context.Response.StatusCode);
        Assert.Equal("Gateway.Unavailable", ProblemCode(context));
    }

    [Fact]
    public async Task When_identity_cannot_be_reached_the_request_is_refused_not_forwarded()
    {
        _tokens.Failure = new HttpRequestException("identity down");
        var context = Context(ApiRoute());

        await Run(context);

        Assert.False(_nextCalled);
        Assert.Equal(503, context.Response.StatusCode);
    }

    [Fact]
    public async Task A_timeout_to_identity_is_a_503_but_a_client_that_went_away_is_left_alone()
    {
        _tokens.Failure = new TaskCanceledException("timeout");
        var timedOut = Context(ApiRoute());
        await Run(timedOut);
        Assert.Equal(503, timedOut.Response.StatusCode);

        using var cts = new CancellationTokenSource();
        var gone = Context(ApiRoute());
        gone.RequestAborted = cts.Token;
        await cts.CancelAsync();

        await Assert.ThrowsAsync<TaskCanceledException>(() => Run(gone));
    }

    // ---- identity (the token is forwarded unchanged)

    [Fact]
    public async Task A_pass_through_project_lets_an_anonymous_call_in_for_login_and_register()
    {
        var context = Context(ApiRoute("PassThrough"), authenticated: false);

        await Run(context);

        Assert.True(_nextCalled);
        Assert.Empty(_access.Asked);
        Assert.Empty(_tokens.Asked);
    }

    [Fact]
    public async Task A_pass_through_project_refuses_a_token_that_is_present_but_invalid()
    {
        var context = Context(ApiRoute("PassThrough"), authenticated: false);
        context.Request.Headers.Authorization = "Bearer garbage";

        await Run(context);

        Assert.False(_nextCalled);
        Assert.Equal(401, context.Response.StatusCode);
        Assert.Equal("Auth.InvalidToken", ProblemCode(context));
    }

    [Fact]
    public async Task A_pass_through_project_gets_the_edge_token_unchanged_and_no_forged_headers()
    {
        var context = Context(ApiRoute("PassThrough"));
        context.Request.Headers.Authorization = "Bearer edge-token";
        AddForgedHeaders(context);

        await Run(context);

        Assert.True(_nextCalled);
        Assert.Equal("Bearer edge-token", context.Request.Headers.Authorization.ToString());
        AssertNoForgedHeaders(context);
        Assert.Empty(_tokens.Asked);
    }
}
