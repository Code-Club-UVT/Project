using CoreApi.Entities;
using CoreApi.Enums;
using CoreApi.Options;
using CoreApi.Repositories.Abstractions;
using CoreApi.Services.Projects;
using Microsoft.Extensions.Options;

namespace CoreApi.Services.Seeding;

/// <summary>
/// Creates the "core" project (this API behind the gateway) and keeps its URLs in line with <c>Seed:CoreProject</c>,
/// because the address differs between machines (localhost, Docker). Nothing is created without a base URL.
/// A failure (for example the database is not reachable yet) is logged and never stops the app from starting.
/// </summary>
public class ProjectSeeder(
    IServiceScopeFactory scopeFactory,
    IOptions<SeedOptions> options,
    TimeProvider timeProvider,
    ILogger<ProjectSeeder> logger) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var core = options.Value.CoreProject;
        if (string.IsNullOrWhiteSpace(core.BaseUrl))
        {
            logger.LogWarning("Core project seeding skipped: Seed:CoreProject:BaseUrl is not set, so the gateway will not know where this API is");
            return;
        }

        var baseUrl = core.BaseUrl.Trim();
        var healthUrl = string.IsNullOrWhiteSpace(core.HealthUrl) ? null : core.HealthUrl.Trim();

        var urlError = ProjectUrlRules.Validate(baseUrl, "Seed:CoreProject:BaseUrl", allowLocalAddresses: true)
            ?? (healthUrl is null ? null : ProjectUrlRules.Validate(healthUrl, "Seed:CoreProject:HealthUrl", allowLocalAddresses: true));
        if (urlError is not null)
        {
            logger.LogWarning("Core project seeding skipped: {Message}", urlError.Message);
            return;
        }

        try
        {
            using var scope = scopeFactory.CreateScope();
            var projects = scope.ServiceProvider.GetRequiredService<IProjectRepository>();
            var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

            var now = timeProvider.GetUtcNow().UtcDateTime;
            var project = await projects.GetBySlugAsync(ProjectService.CoreSlug, cancellationToken);
            if (project is null)
            {
                await projects.CreateAsync(new Project
                {
                    Id = Guid.NewGuid(),
                    Slug = ProjectService.CoreSlug,
                    Name = "Core API",
                    Description = "Accounts, authentication and project access",
                    BaseUrl = baseUrl,
                    HealthUrl = healthUrl,
                    TokenMode = ProjectTokenMode.PassThrough,
                    IsActive = true,
                    IsSystem = true,
                    CreatedAt = now,
                    UpdatedAt = now
                }, cancellationToken);
                await unitOfWork.SaveChangesAsync(cancellationToken);
                logger.LogInformation("Seeded the core project at {BaseUrl}", baseUrl);
            }
            else if (project.BaseUrl != baseUrl || project.HealthUrl != healthUrl)
            {
                project.BaseUrl = baseUrl;
                project.HealthUrl = healthUrl;
                project.UpdatedAt = now;
                await unitOfWork.SaveChangesAsync(cancellationToken);
                logger.LogInformation("Updated the core project URLs from configuration ({BaseUrl})", baseUrl);
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Core project seeding skipped: the database is not available or not migrated");
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
