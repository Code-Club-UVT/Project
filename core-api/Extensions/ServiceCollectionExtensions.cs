using CoreApi.Common;
using CoreApi.Data.Postgres;
using CoreApi.Data.Postgres.Repositories;
using CoreApi.Options;
using CoreApi.Repositories.Abstractions;
using CoreApi.Services.Auth;
using CoreApi.Services.Email;
using CoreApi.Services.ProjectMembers;
using CoreApi.Services.ProjectRoles;
using CoreApi.Services.Projects;
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
        services.AddScoped<IProjectRepository, PostgresProjectRepository>();
        services.AddScoped<IProjectRoleRepository, PostgresProjectRoleRepository>();
        services.AddScoped<IProjectMemberRepository, PostgresProjectMemberRepository>();

        return services;
    }

    public static IServiceCollection AddServices(this IServiceCollection services, IHostEnvironment environment, IConfiguration configuration)
    {
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<ITokenService, TokenService>();

        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IAccountService, AccountService>();
        services.AddScoped<IUserService, UserService>();
        services.AddScoped<IProjectService, ProjectService>();
        services.AddScoped<IProjectRoleService, ProjectRoleService>();
        services.AddScoped<IProjectMemberService, ProjectMemberService>();
        services.AddScoped<IProjectTokenService, ProjectTokenService>();

        services.AddHealthChecks().AddCheck<PostgresHealthCheck>("postgres");

        services.AddOptions<SmtpOptions>()
            .BindConfiguration(SmtpOptions.SectionName)
            .Validate(IsValidSmtp, "Smtp is enabled but Host, Port, Username, Password and FromAddress must be set, and Password must not be the CHANGE-ME placeholder.")
            .ValidateOnStart();

        if (configuration.GetValue<bool>($"{SmtpOptions.SectionName}:Enabled"))
        {
            services.AddSingleton<IEmailSender, SmtpEmailSender>();
        }
        else if (environment.IsDevelopment())
        {
            services.AddSingleton<IEmailSender, LoggingEmailSender>();
        }
        else
        {
            throw new InvalidOperationException(
                "No email sender is configured. Set Smtp__Enabled=true and the other Smtp__* settings.");
        }

        services.AddHostedService<AdminSeeder>();
        services.AddHostedService<ProjectSeeder>();

        return services;
    }

    private static bool IsValidSmtp(SmtpOptions o) =>
        !o.Enabled
        || (!string.IsNullOrWhiteSpace(o.Host)
            && o.Port is > 0 and <= 65535
            && !string.IsNullOrWhiteSpace(o.Username)
            && !string.IsNullOrWhiteSpace(o.Password)
            && !o.Password.StartsWith(SmtpOptions.PlaceholderPrefix, StringComparison.Ordinal)
            && !string.IsNullOrWhiteSpace(o.FromAddress)
            && o.TimeoutSeconds > 0);
}
