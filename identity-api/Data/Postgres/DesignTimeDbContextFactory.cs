using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Identity.Data.Postgres;

/// <summary>
/// Used by <c>dotnet ef</c> and the migration bundle, so neither has to start the app (which needs a signing key and
/// more). The connection string is a placeholder; the bundle gets the real one with <c>--connection</c>.
/// </summary>
public class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<PostgresDbContext>
{
    public PostgresDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<PostgresDbContext>()
            .UseNpgsql(
                "Host=localhost;Port=5432;Database=platform;Username=postgres;Password=postgres",
                npgsql => npgsql.MigrationsHistoryTable("__EFMigrationsHistory", PostgresDbContext.Schema))
            .Options;

        return new PostgresDbContext(options);
    }
}
