using Gateway.Data;
using Gateway.Routing;
using Xunit;

namespace Gateway.Tests;

public class ProxyConfigBuilderTests
{
    private static ProjectRow Row(
        string slug = "foo",
        string? backend = "http://foo-api:8080/api",
        string? frontend = null,
        string? health = null,
        string tokenMode = "Exchange") => new()
    {
        Id = Guid.NewGuid(),
        Slug = slug,
        BackendUrl = backend,
        FrontendUrl = frontend,
        HealthUrl = health,
        TokenMode = tokenMode,
        IsActive = true,
        UpdatedAt = new DateTime(2026, 11, 8, 0, 0, 0, DateTimeKind.Utc)
    };

    private static ProjectConfig Build(ProjectRow row)
    {
        Assert.True(ProxyConfigBuilder.TryBuild(row, out var config, out var problem), problem);
        return config!;
    }

    [Fact]
    public void BackendOnly_BuildsApiRouteOnly()
    {
        var config = Build(Row(backend: "http://foo-api:8080/api", frontend: null));

        var route = Assert.Single(config.Routes);
        Assert.Equal("/api/foo/{**rest}", route.Match.Path);
        Assert.Equal("foo", route.RouteId);
        Assert.False(route.Metadata!.ContainsKey(ProxyConfigBuilder.RouteKindKey));
        var cluster = Assert.Single(config.Clusters);
        Assert.Equal("foo", cluster.ClusterId);
        Assert.Equal("http://foo-api:8080/api", cluster.Destinations!["main"].Address);
    }

    [Fact]
    public void FrontendOnly_BuildsAppsRouteOnly()
    {
        var config = Build(Row(backend: null, frontend: "http://foo-web:8080/"));

        var route = Assert.Single(config.Routes);
        Assert.Equal("/apps/foo/{**rest}", route.Match.Path);
        Assert.Equal("foo/web", route.RouteId);
        Assert.Equal("foo/web", route.ClusterId);
        Assert.Equal("anonymous", route.AuthorizationPolicy);
        Assert.Equal(ProxyConfigBuilder.RouteKindFrontend, route.Metadata![ProxyConfigBuilder.RouteKindKey]);
        Assert.Equal("foo", route.Metadata[ProxyConfigBuilder.SlugKey]);
        Assert.Equal("/apps/foo", Assert.Single(route.Transforms!)["PathRemovePrefix"]);
        var cluster = Assert.Single(config.Clusters);
        Assert.Equal("foo/web", cluster.ClusterId);
        Assert.Equal("http://foo-web:8080/", cluster.Destinations!["main"].Address);
        Assert.Null(cluster.HealthCheck);
    }

    [Fact]
    public void Both_BuildsTwoRoutesAndTwoClusters()
    {
        var config = Build(Row(frontend: "http://foo-web:8080/"));

        Assert.Equal(["foo", "foo/web"], config.Routes.Select(r => r.RouteId).Order().ToArray());
        Assert.Equal(["foo", "foo/web"], config.Clusters.Select(c => c.ClusterId).Order().ToArray());
    }

    [Fact]
    public void Neither_BuildsNothing_AndSucceeds()
    {
        var config = Build(Row(backend: null, frontend: null));

        Assert.Empty(config.Routes);
        Assert.Empty(config.Clusters);
    }

    [Theory]
    [InlineData("ftp://foo-web/")]
    [InlineData("not a url")]
    [InlineData("/relative")]
    public void InvalidFrontendUrl_IsReportedAndSkipped_WithoutThrowing(string frontend)
    {
        var ok = ProxyConfigBuilder.TryBuild(Row(frontend: frontend), out var config, out var problem);

        Assert.False(ok);
        Assert.Null(config);
        Assert.Contains("FrontendUrl", problem);
    }

    [Fact]
    public void InvalidBackendUrl_IsReportedAndSkipped_WithoutThrowing()
    {
        var ok = ProxyConfigBuilder.TryBuild(Row(backend: "ftp://foo-api/"), out var config, out var problem);

        Assert.False(ok);
        Assert.Null(config);
        Assert.Contains("BackendUrl", problem);
    }

    [Fact]
    public void BlankUrls_AreTreatedAsMissing()
    {
        var config = Build(Row(backend: "  ", frontend: ""));

        Assert.Empty(config.Routes);
    }

    [Fact]
    public void InvalidSlug_IsRejected()
    {
        Assert.False(ProxyConfigBuilder.TryBuild(Row(slug: "Bad_Slug"), out _, out var problem));
        Assert.Contains("slug", problem);
    }

    [Fact]
    public void PassThroughNonIdentityProject_IsStillPassThroughOnApiRoute_ButNeverOnFrontend()
    {
        // Core forbids PassThrough outside system projects; this pins what the gateway does if a row ever says so.
        var config = Build(Row(tokenMode: "PassThrough", frontend: "http://foo-web:8080/"));

        var api = config.Routes.Single(r => r.RouteId == "foo");
        var web = config.Routes.Single(r => r.RouteId == "foo/web");
        Assert.Equal(ProxyConfigBuilder.PassThrough, api.Metadata![ProxyConfigBuilder.TokenModeKey]);
        Assert.Equal(ProxyConfigBuilder.RouteKindFrontend, web.Metadata![ProxyConfigBuilder.RouteKindKey]);
    }

    [Fact]
    public void IdentityProject_StillGetsStricterAuthRoute()
    {
        var config = Build(Row(slug: "identity", backend: "http://identity-api:8080/api", tokenMode: "PassThrough"));

        Assert.Equal(["identity", "identity/auth"], config.Routes.Select(r => r.RouteId).Order().ToArray());
        var auth = config.Routes.Single(r => r.RouteId == "identity/auth");
        Assert.Equal(ProxyConfigBuilder.AuthRateLimitPolicy, auth.RateLimiterPolicy);
        Assert.Single(config.Clusters);
    }

    [Fact]
    public void Fingerprint_ChangesWhenFrontendOrBackendUrlChanges()
    {
        var row = Row(frontend: "http://foo-web:8080/");
        var before = ProxyConfigBuilder.Fingerprint([row]);

        row.FrontendUrl = "http://foo-web2:8080/";
        var afterFrontend = ProxyConfigBuilder.Fingerprint([row]);
        row.BackendUrl = "http://other:8080/api";
        var afterBackend = ProxyConfigBuilder.Fingerprint([row]);

        Assert.NotEqual(before, afterFrontend);
        Assert.NotEqual(afterFrontend, afterBackend);
    }
}
