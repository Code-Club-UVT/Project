using Gateway.Data;
using Gateway.Options;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Yarp.ReverseProxy.Configuration;

namespace Gateway.Routing;

/// <summary>
/// Reads the active projects from the database every <see cref="RoutingOptions.PollSeconds"/> and replaces the proxy
/// configuration when something changed. A project whose row cannot be routed (bad URL, something YARP would reject) is
/// skipped with a warning, so one bad row never stops the others from being updated. If the database cannot be read the
/// current routes stay.
/// </summary>
public sealed class ProjectConfigPoller(
    IDbContextFactory<GatewayDbContext> dbFactory,
    DbProxyConfigProvider configProvider,
    IConfigValidator validator,
    IOptions<RoutingOptions> options,
    ILogger<ProjectConfigPoller> logger) : BackgroundService
{
    private string? _fingerprint;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(options.Value.PollSeconds));
        do
        {
            await RefreshAsync(stoppingToken);
        }
        while (await WaitAsync(timer, stoppingToken));
    }

    private async Task RefreshAsync(CancellationToken cancellationToken)
    {
        try
        {
            await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
            var projects = await db.Projects.AsNoTracking().Where(p => p.IsActive).OrderBy(p => p.Slug).ToListAsync(cancellationToken);

            var fingerprint = ProxyConfigBuilder.Fingerprint(projects);
            if (fingerprint == _fingerprint)
            {
                return;
            }

            var routes = new List<RouteConfig>();
            var clusters = new List<ClusterConfig>();
            var routed = new List<string>();
            foreach (var project in projects)
            {
                var routesBefore = routes.Count;
                var problem = await TryAddAsync(project, routes, clusters);
                if (problem is null)
                {
                    // A project with neither a backend nor a frontend is valid but has nothing to route.
                    if (routes.Count > routesBefore)
                    {
                        routed.Add(project.Slug);
                    }
                }
                else
                {
                    logger.LogWarning("Project {Slug} is not routed: {Problem}", project.Slug, problem);
                }
            }

            configProvider.Update(routes, clusters);
            _fingerprint = fingerprint;
            logger.LogInformation("Routes updated: {Routed} of {Total} active project(s) routed ({Slugs})", routed.Count, projects.Count, string.Join(", ", routed));
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Could not read the projects; keeping the current routes ({State})", _fingerprint is null ? "none loaded yet" : "loaded earlier");
        }
    }

    /// <summary>Adds the project's routes and cluster if they are valid; otherwise returns why not.</summary>
    private async Task<string?> TryAddAsync(ProjectRow project, List<RouteConfig> routes, List<ClusterConfig> clusters)
    {
        if (!ProxyConfigBuilder.TryBuild(project, out var config, out var problem))
        {
            return problem;
        }

        // Ask YARP itself, because it rejects the whole configuration if any single route or cluster is invalid.
        var errors = new List<Exception>();
        foreach (var route in config!.Routes)
        {
            errors.AddRange(await validator.ValidateRouteAsync(route));
        }

        foreach (var cluster in config.Clusters)
        {
            errors.AddRange(await validator.ValidateClusterAsync(cluster));
        }

        if (errors.Count > 0)
        {
            return string.Join("; ", errors.Select(e => e.Message));
        }

        routes.AddRange(config.Routes);
        clusters.AddRange(config.Clusters);
        return null;
    }

    private static async Task<bool> WaitAsync(PeriodicTimer timer, CancellationToken cancellationToken)
    {
        try
        {
            return await timer.WaitForNextTickAsync(cancellationToken);
        }
        catch (OperationCanceledException)
        {
            return false;
        }
    }
}
