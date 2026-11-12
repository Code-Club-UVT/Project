using System.Text.RegularExpressions;
using Gateway.Data;
using Yarp.ReverseProxy.Configuration;
using Yarp.ReverseProxy.Health;

namespace Gateway.Routing;

/// <summary>
/// The YARP routes and clusters of one project: the API route and cluster (when it has a backend) and the frontend
/// route and cluster (when it has a frontend). Both lists are empty when it has neither.
/// </summary>
public sealed record ProjectConfig(IReadOnlyList<RouteConfig> Routes, IReadOnlyList<ClusterConfig> Clusters);

/// <summary>Turns the active projects into YARP routes and clusters. Pure, so it can be reasoned about on its own.</summary>
public static class ProxyConfigBuilder
{
    public const string AuthRateLimitPolicy = "per-ip-auth";
    public const string DefaultRateLimitPolicy = "per-user";
    public const string CorsPolicy = "frontend";

    public const string SlugKey = "Slug";
    public const string ProjectIdKey = "ProjectId";
    public const string TokenModeKey = "TokenMode";
    public const string PassThrough = "PassThrough";
    public const string Exchange = "Exchange";

    /// <summary>Route metadata: <see cref="RouteKindFrontend"/> for the static frontend route; absent for API routes.</summary>
    public const string RouteKindKey = "RouteKind";
    public const string RouteKindFrontend = "frontend";

    private const string IdentitySlug = "identity";

    /// <summary>Route and cluster id of a project's frontend. The '/' cannot be part of a slug, so it never collides.</summary>
    public static string FrontendId(string slug) => slug + "/web";

    private static readonly Regex SlugPattern = new("^[a-z0-9]+(-[a-z0-9]+)*$", RegexOptions.Compiled);

    /// <summary>
    /// Builds the routes and the cluster of one project. Returns false, with the reason, when the row cannot be routed
    /// (so the caller skips that project and the others keep working).
    /// </summary>
    public static bool TryBuild(ProjectRow project, out ProjectConfig? config, out string? problem)
    {
        config = null;

        if (!SlugPattern.IsMatch(project.Slug))
        {
            problem = "the slug is not valid";
            return false;
        }

        var hasBackend = !string.IsNullOrWhiteSpace(project.BackendUrl);
        var hasFrontend = !string.IsNullOrWhiteSpace(project.FrontendUrl);

        if (hasBackend && !IsHttpUrl(project.BackendUrl!))
        {
            problem = "BackendUrl is not an absolute http(s) URL";
            return false;
        }

        if (hasFrontend && !IsHttpUrl(project.FrontendUrl!))
        {
            problem = "FrontendUrl is not an absolute http(s) URL";
            return false;
        }

        if (!string.IsNullOrWhiteSpace(project.HealthUrl) && !IsHttpUrl(project.HealthUrl))
        {
            problem = "HealthUrl is not an absolute http(s) URL";
            return false;
        }

        var routes = new List<RouteConfig>();
        var clusters = new List<ClusterConfig>();

        if (hasBackend)
        {
            var passThrough = project.TokenMode == PassThrough;
            var prefix = "/api/" + project.Slug;
            var metadata = new Dictionary<string, string>
            {
                [SlugKey] = project.Slug,
                [ProjectIdKey] = project.Id.ToString(),
                [TokenModeKey] = passThrough ? PassThrough : Exchange
            };

            // Pass-through projects (identity) decide on authentication themselves: login and register are anonymous there.
            // The gateway still rejects a token that is present but invalid (see ProjectAccessMiddleware).
            var authorizationPolicy = passThrough ? "anonymous" : "default";

            routes.Add(NewRoute(project.Slug, project.Slug, prefix + "/{**rest}", prefix, DefaultRateLimitPolicy, authorizationPolicy, metadata, order: 0));

            if (project.Slug == IdentitySlug)
            {
                // Stricter limit for login, register, password reset, ... (per IP). The id contains a '/', which a slug cannot,
                // so it can never collide with the route of a project.
                routes.Add(NewRoute(project.Slug + "/auth", project.Slug, prefix + "/auth/{**rest}", prefix, AuthRateLimitPolicy, authorizationPolicy, metadata, order: -1));
            }

            clusters.Add(NewBackendCluster(project));
        }

        if (hasFrontend)
        {
            // The static frontend: public files (an iframe navigation cannot send an Authorization header), so the route is
            // anonymous and ProjectAccessMiddleware lets it through. The API behind the app is what is protected.
            var prefix = "/apps/" + project.Slug;
            var metadata = new Dictionary<string, string>
            {
                [SlugKey] = project.Slug,
                [ProjectIdKey] = project.Id.ToString(),
                [RouteKindKey] = RouteKindFrontend
            };

            routes.Add(NewRoute(FrontendId(project.Slug), FrontendId(project.Slug), prefix + "/{**rest}", prefix, DefaultRateLimitPolicy, "anonymous", metadata, order: 0));
            clusters.Add(NewFrontendCluster(project));
        }

        config = new ProjectConfig(routes, clusters);
        problem = null;
        return true;
    }

    private static bool IsHttpUrl(string value)
    {
        return Uri.TryCreate(value, UriKind.Absolute, out var uri) && uri.Scheme is "http" or "https";
    }

    /// <summary>Changes whenever something that affects the routes changes; used to avoid reloading identical config.</summary>
    public static string Fingerprint(IEnumerable<ProjectRow> projects)
    {
        return string.Join(
            '|',
            projects.OrderBy(p => p.Id).Select(p => $"{p.Id}/{p.Slug}/{p.BackendUrl}/{p.FrontendUrl}/{p.HealthUrl}/{p.TokenMode}/{p.UpdatedAt.Ticks}"));
    }

    private static RouteConfig NewRoute(
        string routeId,
        string clusterId,
        string path,
        string prefixToRemove,
        string rateLimitPolicy,
        string authorizationPolicy,
        Dictionary<string, string> metadata,
        int order)
    {
        return new RouteConfig
        {
            RouteId = routeId,
            ClusterId = clusterId,
            Order = order,
            Match = new RouteMatch { Path = path },
            AuthorizationPolicy = authorizationPolicy,
            RateLimiterPolicy = rateLimitPolicy,
            CorsPolicy = CorsPolicy,
            Metadata = metadata,
            Transforms = [new Dictionary<string, string> { ["PathRemovePrefix"] = prefixToRemove }]
        };
    }

    private static ClusterConfig NewFrontendCluster(ProjectRow project)
    {
        return new ClusterConfig
        {
            ClusterId = FrontendId(project.Slug),
            Destinations = new Dictionary<string, DestinationConfig>
            {
                ["main"] = new() { Address = project.FrontendUrl!.Trim() }
            }
        };
    }

    private static ClusterConfig NewBackendCluster(ProjectRow project)
    {
        var healthCheck = string.IsNullOrWhiteSpace(project.HealthUrl)
            ? null
            : new HealthCheckConfig
            {
                Active = new ActiveHealthCheckConfig
                {
                    Enabled = true,
                    Interval = TimeSpan.FromSeconds(30),
                    Timeout = TimeSpan.FromSeconds(5),
                    Policy = HealthCheckConstants.ActivePolicy.ConsecutiveFailures
                }
            };

        return new ClusterConfig
        {
            ClusterId = project.Slug,
            HealthCheck = healthCheck,
            Destinations = new Dictionary<string, DestinationConfig>
            {
                ["main"] = new() { Address = project.BackendUrl!.Trim(), Health = string.IsNullOrWhiteSpace(project.HealthUrl) ? null : project.HealthUrl }
            }
        };
    }
}
