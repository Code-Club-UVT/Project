using CoreApi.Common;
using CoreApi.Data.Postgres;
using CoreApi.Data.Postgres.Repositories;
using CoreApi.Options;
using CoreApi.Repositories.Abstractions;
using CoreApi.Services.ProjectMembers;
using CoreApi.Services.ProjectRoles;
using CoreApi.Services.Projects;
using CoreApi.Services.Seeding;
using CoreApi.Services.Users;
using Microsoft.EntityFrameworkCore;
using PlatformKit;

namespace CoreApi.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddPostgres(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<PostgresDbContext>(options => options.UseNpgsql(
            configuration.GetConnectionString("PostgresConnection"),
            npgsql => npgsql.MigrationsHistoryTable("__EFMigrationsHistory", PostgresDbContext.Schema)));

        return services;
    }

    public static IServiceCollection AddRepositories(this IServiceCollection services)
    {
        services.AddScoped<IUnitOfWork, PostgresUnitOfWork>();
        services.AddScoped(typeof(IRepository<,>), typeof(PostgresRepository<,>));
        services.AddScoped<IUserProfileRepository, PostgresUserProfileRepository>();
        services.AddScoped<IProjectRepository, PostgresProjectRepository>();
        services.AddScoped<IProjectRoleRepository, PostgresProjectRoleRepository>();
        services.AddScoped<IProjectMemberRepository, PostgresProjectMemberRepository>();

        return services;
    }

    public static IServiceCollection AddServices(this IServiceCollection services)
    {
        services.AddSingleton(TimeProvider.System);

        services.AddScoped<IUserService, UserService>();
        services.AddScoped<IUserProvisioningService, UserProvisioningService>();
        services.AddScoped<IProjectService, ProjectService>();
        services.AddScoped<IProjectRoleService, ProjectRoleService>();
        services.AddScoped<IProjectMemberService, ProjectMemberService>();

        services.AddOptions<SeedOptions>().BindConfiguration(SeedOptions.SectionName);
        services.AddHostedService<ProjectSeeder>();

        services.AddPlatformHealth().AddCheck<PostgresHealthCheck>("postgres");

        return services;
    }

    /// <summary>
    /// Project tokens for <c>core</c> (default scheme, validated by platform-kit; the role is the user's core role, so
    /// <c>Admin</c> means platform admin) and identity's internal key for <c>/internal/users</c>.
    /// </summary>
    public static IServiceCollection AddAuth(this IServiceCollection services, IConfiguration configuration, IHostEnvironment environment)
    {
        services.AddPlatformAuthentication(configuration);
        services.AddAuthentication().AddInternalKeyAuthentication(environment);

        return services;
    }
}
