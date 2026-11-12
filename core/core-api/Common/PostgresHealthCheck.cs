using CoreApi.Data.Postgres;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace CoreApi.Common;

public class PostgresHealthCheck(PostgresDbContext context) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext healthContext, CancellationToken cancellationToken = default)
    {
        return await context.Database.CanConnectAsync(cancellationToken)
            ? HealthCheckResult.Healthy()
            : HealthCheckResult.Unhealthy("PostgreSQL is not reachable.");
    }
}
