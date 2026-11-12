using PlatformKit.Results;
using CoreApi.Entities;
using CoreApi.Enums;
using CoreApi.Mappings;
using CoreApi.Models.Requests;
using CoreApi.Models.Responses;
using CoreApi.Repositories.Abstractions;

namespace CoreApi.Services.Projects;

public class ProjectService(
    IProjectRepository projects,
    IProjectRoleRepository roles,
    IUnitOfWork unitOfWork,
    IHostEnvironment environment,
    TimeProvider timeProvider) : IProjectService
{
    public const string CoreSlug = "core";
    public const string IdentitySlug = "identity";

    // Slugs that would be confusing as /api/<slug>; "core" and "identity" are created by the app only.
    private static readonly HashSet<string> ReservedSlugs = [CoreSlug, IdentitySlug, "email", "gateway", "internal", "health", "admin"];

    public async Task<Result<PagedResult<ProjectResponse>>> GetPagedAsync(int page, int pageSize, CancellationToken cancellationToken = default)
    {
        var result = await projects.GetPagedAsync(page, pageSize, cancellationToken);
        var items = result.Items.Select(p => p.ToResponse()).ToList();

        return Result.Success(new PagedResult<ProjectResponse>(items, result.Page, result.PageSize, result.TotalCount));
    }

    public async Task<Result<ProjectResponse>> GetByIdAsync(Guid projectId, CancellationToken cancellationToken = default)
    {
        var project = await projects.GetByIdAsync(projectId, cancellationToken);

        return project is null ? ProjectErrors.NotFound : Result.Success(project.ToResponse());
    }

    public async Task<Result<ProjectResponse>> CreateAsync(CreateProjectRequest request, CancellationToken cancellationToken = default)
    {
        var slug = request.Slug.Trim().ToLowerInvariant();
        if (ReservedSlugs.Contains(slug))
        {
            return ProjectErrors.SlugReserved;
        }

        var urlError = ValidateUrls(request.BackendUrl, request.FrontendUrl, request.HealthUrl);
        if (urlError is not null)
        {
            return urlError;
        }

        if (await projects.SlugExistsAsync(slug, cancellationToken))
        {
            return ProjectErrors.SlugTaken;
        }

        var now = timeProvider.GetUtcNow().UtcDateTime;
        var project = new Project
        {
            Id = Guid.NewGuid(),
            Slug = slug,
            Name = request.Name.Trim(),
            Description = NullIfBlank(request.Description),
            BackendUrl = NullIfBlank(request.BackendUrl),
            FrontendUrl = NullIfBlank(request.FrontendUrl),
            HealthUrl = NullIfBlank(request.HealthUrl),
            TokenMode = ProjectTokenMode.Exchange,
            CreatedAt = now,
            UpdatedAt = now
        };

        await projects.CreateAsync(project, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(project.ToResponse());
    }

    public async Task<Result<ProjectResponse>> UpdateAsync(Guid projectId, UpdateProjectRequest request, CancellationToken cancellationToken = default)
    {
        var project = await projects.GetByIdAsync(projectId, cancellationToken);
        if (project is null)
        {
            return ProjectErrors.NotFound;
        }

        var backendUrl = NullIfBlank(request.BackendUrl);
        var frontendUrl = NullIfBlank(request.FrontendUrl);
        var healthUrl = NullIfBlank(request.HealthUrl);

        if (project.IsSystem)
        {
            // The core project's URLs come from configuration (see ProjectSeeder) and it must stay reachable.
            if (backendUrl != project.BackendUrl || frontendUrl != project.FrontendUrl || healthUrl != project.HealthUrl || !request.IsActive)
            {
                return ProjectErrors.SystemProtected;
            }
        }
        else
        {
            var urlError = ValidateUrls(backendUrl, frontendUrl, healthUrl);
            if (urlError is not null)
            {
                return urlError;
            }
        }

        if (request.DefaultRoleId is { } defaultRoleId)
        {
            if (project.TokenMode == ProjectTokenMode.PassThrough)
            {
                return ProjectErrors.NoMemberships;
            }

            var role = await roles.GetByIdAsync(defaultRoleId, cancellationToken);
            if (role is null || role.ProjectId != project.Id)
            {
                return ProjectErrors.InvalidDefaultRole;
            }
        }

        project.DefaultRoleId = request.DefaultRoleId;
        project.Name = request.Name.Trim();
        project.Description = NullIfBlank(request.Description);
        project.BackendUrl = backendUrl;
        project.FrontendUrl = frontendUrl;
        project.HealthUrl = healthUrl;
        project.IsActive = request.IsActive;
        project.UpdatedAt = timeProvider.GetUtcNow().UtcDateTime;
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(project.ToResponse());
    }

    public async Task<Result> DeleteAsync(Guid projectId, CancellationToken cancellationToken = default)
    {
        var project = await projects.GetByIdAsync(projectId, cancellationToken);
        if (project is null)
        {
            return ProjectErrors.NotFound;
        }

        if (project.IsSystem)
        {
            return ProjectErrors.SystemProtected;
        }

        // The project points at its default role and the role at the project: break that first, so the cascade below
        // never has to update the row it is deleting.
        if (project.DefaultRoleId is not null)
        {
            project.DefaultRoleId = null;
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }

        // Roles and members go with the project (cascade).
        await projects.DeleteAsync(project, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }

    /// <summary>Each URL is optional (a project may have a backend, a frontend, both or neither); the ones given must be valid.</summary>
    private Error? ValidateUrls(string? backendUrl, string? frontendUrl, string? healthUrl)
    {
        var allowLocal = environment.IsDevelopment();

        if (string.IsNullOrWhiteSpace(backendUrl) && !string.IsNullOrWhiteSpace(healthUrl))
        {
            return ProjectErrors.InvalidUrl("HealthUrl needs a BackendUrl: it is the health check of the backend.");
        }

        foreach (var (url, label) in new[] { (backendUrl, "BackendUrl"), (frontendUrl, "FrontendUrl"), (healthUrl, "HealthUrl") })
        {
            if (string.IsNullOrWhiteSpace(url))
            {
                continue;
            }

            var error = ProjectUrlRules.Validate(url.Trim(), label, allowLocal);
            if (error is not null)
            {
                return error;
            }
        }

        return null;
    }

    private static string? NullIfBlank(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}
