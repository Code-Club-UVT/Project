using CoreApi.Entities;
using CoreApi.Enums;
using CoreApi.Options;
using CoreApi.Repositories.Abstractions;
using CoreApi.Services.Projects;
using Microsoft.Extensions.Options;

namespace CoreApi.Services.Seeding;

/// <summary>
/// Creates the two system projects and keeps their URLs in line with configuration, because the addresses differ
/// between machines (localhost, Docker):
/// <list type="bullet">
/// <item><c>core</c> (this API, Exchange) with the roles <c>User</c> (its default role, so every confirmed user can use
/// it) and <c>Admin</c> (platform admin rights).</item>
/// <item><c>identity</c> (PassThrough: login and register are anonymous, and identity validates its own edge tokens).</item>
/// </list>
/// A project without a base URL is skipped. A failure (for example the database is not reachable yet) is logged and
/// never stops the app from starting.
/// </summary>
public class ProjectSeeder(
    IServiceScopeFactory scopeFactory,
    IOptions<SeedOptions> options,
    TimeProvider timeProvider,
    ILogger<ProjectSeeder> logger) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var seed = options.Value;

        try
        {
            using var scope = scopeFactory.CreateScope();
            var projects = scope.ServiceProvider.GetRequiredService<IProjectRepository>();
            var roles = scope.ServiceProvider.GetRequiredService<IProjectRoleRepository>();
            var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

            var core = await EnsureProjectAsync(projects, unitOfWork, ProjectService.CoreSlug, "Core API",
                "Profiles, projects, roles and access", ProjectTokenMode.Exchange, seed.CoreProject, "Seed:CoreProject", cancellationToken);
            if (core is not null)
            {
                await EnsureCoreRolesAsync(core, roles, unitOfWork, cancellationToken);
            }

            await EnsureProjectAsync(projects, unitOfWork, ProjectService.IdentitySlug, "Identity",
                "Accounts, login and tokens", ProjectTokenMode.PassThrough, seed.IdentityProject, "Seed:IdentityProject", cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Project seeding skipped: the database is not available or not migrated");
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    private async Task<Project?> EnsureProjectAsync(
        IProjectRepository projects, IUnitOfWork unitOfWork, string slug, string name, string description,
        ProjectTokenMode tokenMode, SeedProjectOptions settings, string settingsName, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(settings.BaseUrl))
        {
            logger.LogWarning("Seeding of project {Slug} skipped: {Settings}:BaseUrl is not set, so the gateway will not route it", slug, settingsName);
            return await projects.GetBySlugAsync(slug, cancellationToken);
        }

        var baseUrl = settings.BaseUrl.Trim();
        var healthUrl = string.IsNullOrWhiteSpace(settings.HealthUrl) ? null : settings.HealthUrl.Trim();

        var urlError = ProjectUrlRules.Validate(baseUrl, $"{settingsName}:BaseUrl", allowLocalAddresses: true)
            ?? (healthUrl is null ? null : ProjectUrlRules.Validate(healthUrl, $"{settingsName}:HealthUrl", allowLocalAddresses: true));
        if (urlError is not null)
        {
            logger.LogWarning("Seeding of project {Slug} skipped: {Message}", slug, urlError.Message);
            return await projects.GetBySlugAsync(slug, cancellationToken);
        }

        var now = timeProvider.GetUtcNow().UtcDateTime;
        var project = await projects.GetBySlugAsync(slug, cancellationToken);
        if (project is null)
        {
            project = new Project
            {
                Id = Guid.NewGuid(),
                Slug = slug,
                Name = name,
                Description = description,
                BaseUrl = baseUrl,
                HealthUrl = healthUrl,
                TokenMode = tokenMode,
                IsActive = true,
                IsSystem = true,
                CreatedAt = now,
                UpdatedAt = now
            };
            await projects.CreateAsync(project, cancellationToken);
            await unitOfWork.SaveChangesAsync(cancellationToken);
            logger.LogInformation("Seeded the {Slug} project at {BaseUrl}", slug, baseUrl);
        }
        else if (project.BaseUrl != baseUrl || project.HealthUrl != healthUrl)
        {
            project.BaseUrl = baseUrl;
            project.HealthUrl = healthUrl;
            project.UpdatedAt = now;
            await unitOfWork.SaveChangesAsync(cancellationToken);
            logger.LogInformation("Updated the {Slug} project URLs from configuration ({BaseUrl})", slug, baseUrl);
        }

        return project;
    }

    /// <summary>Creates the missing core roles; the first time, <c>User</c> also becomes the default role. An admin may change the default later.</summary>
    private async Task EnsureCoreRolesAsync(Project core, IProjectRoleRepository roles, IUnitOfWork unitOfWork, CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow().UtcDateTime;
        var created = false;

        foreach (var (roleName, description) in new[]
        {
            (CoreRoles.User, "Can use the platform: own profile and projects"),
            (CoreRoles.Admin, "Manages users, projects, roles and access")
        })
        {
            if (await roles.GetByNameAsync(core.Id, roleName, cancellationToken) is not null)
            {
                continue;
            }

            var role = new ProjectRole
            {
                Id = Guid.NewGuid(),
                ProjectId = core.Id,
                Name = roleName,
                Description = description,
                CreatedAt = now,
                UpdatedAt = now
            };
            await roles.CreateAsync(role, cancellationToken);
            created = true;

            if (roleName == CoreRoles.User && core.DefaultRoleId is null)
            {
                // Saved in a second step: the project and the role point at each other.
                await unitOfWork.SaveChangesAsync(cancellationToken);
                core.DefaultRoleId = role.Id;
                core.UpdatedAt = now;
            }
        }

        if (created)
        {
            await unitOfWork.SaveChangesAsync(cancellationToken);
            logger.LogInformation("Seeded the core project roles");
        }
    }
}
