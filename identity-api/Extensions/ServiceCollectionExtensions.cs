using Identity.Common;
using Identity.Data.CoreRead;
using Identity.Data.Postgres;
using Identity.Data.Postgres.Repositories;
using Identity.Options;
using Identity.Repositories.Abstractions;
using Identity.Services.Auth;
using Identity.Services.Outbox;
using Identity.Services.Seeding;
using Identity.Services.Tokens;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using PlatformKit;

namespace Identity.Extensions;

public static class ServiceCollectionExtensions
{
    /// <summary>Identity's own tables (schema <c>identity</c>) and the read-only view of the core API's project tables (schema <c>core</c>).</summary>
    public static IServiceCollection AddPostgres(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("PostgresConnection");

        services.AddDbContext<PostgresDbContext>(options => options.UseNpgsql(
            connectionString,
            npgsql => npgsql.MigrationsHistoryTable("__EFMigrationsHistory", PostgresDbContext.Schema)));
        services.AddDbContext<CoreReadDbContext>(options => options
            .UseNpgsql(connectionString)
            .UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking));

        services.AddPlatformHealth().AddCheck<PostgresHealthCheck>("postgres");

        return services;
    }

    public static IServiceCollection AddRepositories(this IServiceCollection services)
    {
        services.AddScoped<IUnitOfWork, PostgresUnitOfWork>();
        services.AddScoped(typeof(IRepository<,>), typeof(PostgresRepository<,>));
        services.AddScoped<IUserRepository, PostgresUserRepository>();
        services.AddScoped<IRefreshTokenRepository, PostgresRefreshTokenRepository>();
        services.AddScoped<IUserTokenRepository, PostgresUserTokenRepository>();
        services.AddScoped<IOutboxRepository, PostgresOutboxRepository>();
        services.AddScoped<IProjectAccessReader, ProjectAccessReader>();

        return services;
    }

    public static IServiceCollection AddServices(this IServiceCollection services)
    {
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<ITokenService, TokenService>();

        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IAccountService, AccountService>();
        services.AddScoped<IProjectTokenService, ProjectTokenService>();
        services.AddScoped<IOutbox, Outbox>();

        services.AddHostedService<AdminSeeder>();

        return services;
    }

    /// <summary>The outbox dispatcher and one internal HTTP client per target service (<c>Services:Core</c>, <c>Services:Email</c>).</summary>
    public static IServiceCollection AddOutbox(this IServiceCollection services, IConfiguration configuration, IHostEnvironment environment)
    {
        services.AddOptions<OutboxOptions>().BindConfiguration(OutboxOptions.SectionName).ValidateDataAnnotations().ValidateOnStart();
        services.AddOptions<ServicesOptions>().BindConfiguration(ServicesOptions.SectionName).ValidateDataAnnotations().ValidateOnStart();

        var settings = configuration.GetSection(ServicesOptions.SectionName).Get<ServicesOptions>()
            ?? throw new InvalidOperationException("The Services section (Core and Email URLs and keys) is missing.");
        var timeout = TimeSpan.FromSeconds(settings.TimeoutSeconds);

        services.AddInternalClient(OutboxTargets.Core, new Uri(settings.Core.Url), settings.Core.Key, timeout, environment);
        services.AddInternalClient(OutboxTargets.Email, new Uri(settings.Email.Url), settings.Email.Key, timeout, environment);

        services.AddHostedService<OutboxDispatcher>();

        return services;
    }
}
