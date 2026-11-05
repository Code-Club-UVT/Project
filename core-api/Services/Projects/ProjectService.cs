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

        var urlError = ValidateUrls(request.BaseUrl, request.HealthUrl);
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
            BaseUrl = request.BaseUrl.Trim(),
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

        var baseUrl = request.BaseUrl.Trim();
        var healthUrl = NullIfBlank(request.HealthUrl);

        if (project.IsSystem)
        {
            // The core project's URLs come from configuration (see ProjectSeeder) and it must stay reachable.
            if (baseUrl != project.BaseUrl || healthUrl != project.HealthUrl || !request.IsActive)
            {
                return ProjectErrors.SystemProtected;
            }
        }
        else
        {
            var urlError = ValidateUrls(baseUrl, healthUrl);
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
        project.BaseUrl = baseUrl;
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

    private Error? ValidateUrls(string baseUrl, string? healthUrl)
    {
        var allowLocal = environment.IsDevelopment();

        var error = ProjectUrlRules.Validate(baseUrl.Trim(), "BaseUrl", allowLocal);
        if (error is null && !string.IsNullOrWhiteSpace(healthUrl))
        {
            error = ProjectUrlRules.Validate(healthUrl.Trim(), "HealthUrl", allowLocal);
        }

        return error;
    }

    private static string? NullIfBlank(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}
