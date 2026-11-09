using Identity.Controllers;
using Identity.Models.Requests;
using Identity.Models.Responses;
using Identity.Options;
using Identity.Services.Auth;
using Identity.Tests.Support;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.Extensions.Options;
using PlatformKit.Results;
using Xunit;

namespace Identity.Tests;

public class AuthControllerTests
{
    private sealed class FakeProblemDetailsFactory : ProblemDetailsFactory
    {
        public override ProblemDetails CreateProblemDetails(HttpContext httpContext, int? statusCode = null, string? title = null, string? type = null, string? detail = null, string? instance = null)
            => new() { Status = statusCode, Title = title, Detail = detail };

        public override ValidationProblemDetails CreateValidationProblemDetails(HttpContext httpContext, ModelStateDictionary modelStateDictionary, int? statusCode = null, string? title = null, string? type = null, string? detail = null, string? instance = null)
            => new(modelStateDictionary) { Status = statusCode, Title = title, Detail = detail };
    }

    private sealed class FakeAuthService : IAuthService
    {
        public Result<AuthSession> LoginResult { get; set; } = Result.Failure<AuthSession>(AuthErrors.InvalidCredentials);
        public Result<AuthSession> RefreshResult { get; set; } = Result.Failure<AuthSession>(AuthErrors.InvalidRefreshToken);
        public string? RefreshedWith { get; private set; }
        public string? LoggedOutWith { get; private set; }

        public Task<Result<AuthSession>> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default) => Task.FromResult(LoginResult);

        public Task<Result<AuthSession>> RefreshAsync(string? refreshToken, CancellationToken cancellationToken = default)
        {
            RefreshedWith = refreshToken;
            return Task.FromResult(RefreshResult);
        }

        public Task<Result> LogoutAsync(string? refreshToken, CancellationToken cancellationToken = default)
        {
            LoggedOutWith = refreshToken;
            return Task.FromResult(Result.Success());
        }
    }

    private readonly FakeAuthService _auth = new();
    private readonly DefaultHttpContext _http = new();
    private readonly RefreshTokenOptions _cookie = new() { CookieName = "refreshToken", CookiePath = "/dev/api/identity/auth", CookieSecure = true };

    private AuthController Controller()
    {
        var controller = new AuthController(_auth, null!, Opts.Of(_cookie))
        {
            ControllerContext = new ControllerContext { HttpContext = _http },
            ProblemDetailsFactory = new FakeProblemDetailsFactory()
        };
        return controller;
    }

    private static AuthSession Session(string? refresh, DateTime? expires)
        => new(new AuthResponse("access-token", DateTime.UtcNow.AddMinutes(15), new UserResponse(Guid.NewGuid(), "ada", "ada@example.org", true, DateTime.UtcNow)), refresh, expires);

    private string SetCookie() => _http.Response.Headers.SetCookie.ToString();

    [Fact]
    public async Task Login_sets_the_refresh_cookie_hardened_and_keeps_the_token_out_of_the_body()
    {
        _auth.LoginResult = Session("raw-refresh-token", new DateTime(2026, 11, 16, 12, 0, 0, DateTimeKind.Utc));

        var result = await Controller().Login(new LoginRequest { Identifier = "ada", Password = "x" }, default);

        var ok = Assert.IsType<OkObjectResult>(result);
        var body = Assert.IsType<AuthResponse>(ok.Value);
        Assert.Equal("access-token", body.AccessToken);
        Assert.DoesNotContain("raw-refresh-token", System.Text.Json.JsonSerializer.Serialize(body));

        var cookie = SetCookie();
        Assert.StartsWith("refreshToken=raw-refresh-token", cookie);
        Assert.Contains("httponly", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("secure", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("samesite=strict", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("path=/dev/api/identity/auth", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("expires=", cookie, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task A_failed_login_sets_no_cookie_and_maps_the_error()
    {
        var result = await Controller().Login(new LoginRequest { Identifier = "ada", Password = "x" }, default);

        var problem = Assert.IsType<ObjectResult>(result);
        Assert.Equal(401, problem.StatusCode);
        Assert.Equal("", SetCookie());
    }

    [Fact]
    public async Task A_blocked_login_is_a_429_with_its_code()
    {
        _auth.LoginResult = Result.Failure<AuthSession>(AuthErrors.TooManyAttempts);

        var result = await Controller().Login(new LoginRequest { Identifier = "ada", Password = "x" }, default);

        var problem = Assert.IsType<ObjectResult>(result);
        Assert.Equal(429, problem.StatusCode);
        Assert.Equal("Auth.TooManyAttempts", Assert.IsType<ProblemDetails>(problem.Value).Extensions["code"]);
    }

    [Fact]
    public async Task Refresh_reads_the_cookie_and_rotates_it()
    {
        _http.Request.Headers.Cookie = "refreshToken=old-token";
        _auth.RefreshResult = Session("new-token", DateTime.UtcNow.AddDays(7));

        var result = await Controller().Refresh(default);

        Assert.IsType<OkObjectResult>(result);
        Assert.Equal("old-token", _auth.RefreshedWith);
        Assert.StartsWith("refreshToken=new-token", SetCookie());
    }

    [Fact]
    public async Task A_refresh_that_lost_a_race_returns_an_access_token_and_leaves_the_cookie_alone()
    {
        _http.Request.Headers.Cookie = "refreshToken=old-token";
        _auth.RefreshResult = Session(null, null);

        var result = await Controller().Refresh(default);

        Assert.IsType<OkObjectResult>(result);
        Assert.Equal("", SetCookie());
    }

    [Fact]
    public async Task A_refresh_that_fails_clears_the_cookie()
    {
        _http.Request.Headers.Cookie = "refreshToken=bad-token";

        var result = await Controller().Refresh(default);

        Assert.Equal(401, Assert.IsType<ObjectResult>(result).StatusCode);
        var cookie = SetCookie();
        Assert.StartsWith("refreshToken=;", cookie);
        Assert.Contains("1970", cookie);
    }

    [Fact]
    public async Task Logout_revokes_the_cookie_token_and_always_clears_the_cookie()
    {
        _http.Request.Headers.Cookie = "refreshToken=some-token";

        var result = await Controller().Logout(default);

        Assert.IsType<NoContentResult>(result);
        Assert.Equal("some-token", _auth.LoggedOutWith);
        Assert.StartsWith("refreshToken=;", SetCookie());
    }

    [Fact]
    public async Task Logout_without_a_cookie_still_succeeds()
    {
        var result = await Controller().Logout(default);

        Assert.IsType<NoContentResult>(result);
        Assert.Null(_auth.LoggedOutWith);
    }
}
