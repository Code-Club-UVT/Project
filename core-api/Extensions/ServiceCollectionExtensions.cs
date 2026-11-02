using CoreApi.Data.Postgres;
using CoreApi.Data.Postgres.Repositories;
using CoreApi.Repositories.Abstractions;
using CoreApi.Services.Auth;
using CoreApi.Services.Email;
using CoreApi.Services.Seeding;
using CoreApi.Services.Tokens;
using CoreApi.Services.Users;
using Microsoft.EntityFrameworkCore;

namespace CoreApi.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddPostgres(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<PostgresDbContext>(options =>
            options.UseNpgsql(configuration.GetConnectionString("PostgresConnection")));

        return services;
    }

    public static IServiceCollection AddRepositories(this IServiceCollection services)
    {
        services.AddScoped<IUnitOfWork, PostgresUnitOfWork>();
        services.AddScoped(typeof(IRepository<,>), typeof(PostgresRepository<,>));
        services.AddScoped<IUserRepository, PostgresUserRepository>();
        services.AddScoped<IRefreshTokenRepository, PostgresRefreshTokenRepository>();
        services.AddScoped<IUserTokenRepository, PostgresUserTokenRepository>();

        return services;
    }

    public static IServiceCollection AddServices(this IServiceCollection services, IHostEnvironment environment)
    {
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<ITokenService, TokenService>();

        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IAccountService, AccountService>();
        services.AddScoped<IUserService, UserService>();

        if (environment.IsDevelopment())
        {
            services.AddSingleton<IEmailSender, LoggingEmailSender>();
        }
        else
        {
            throw new InvalidOperationException(
                "No IEmailSender is configured for this environment. Register a real implementation in AddServices.");
        }

        services.AddHostedService<AdminSeeder>();

        return services;
    }
}
