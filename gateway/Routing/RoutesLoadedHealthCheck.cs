using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Gateway.Routing;

/// <summary>Not ready until the routes were read from the database once, so a probe does not send traffic to a gateway that would answer 401 to everything.</summary>
public class RoutesLoadedHealthCheck(DbProxyConfigProvider configProvider) : IHealthCheck
{
    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(configProvider.IsLoaded
            ? HealthCheckResult.Healthy()
            : HealthCheckResult.Unhealthy("The routes have not been loaded from the database yet."));
    }
}
