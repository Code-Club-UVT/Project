using CoreApi.Entities;
using CoreApi.Models.Requests;
using CoreApi.Options;
using CoreApi.Repositories.Abstractions;
using CoreApi.Services.Projects;
using Microsoft.Extensions.Options;
using PlatformKit.Results;

namespace CoreApi.Services.Users;

public class UserProvisioningService(
    IUserProfileRepository profiles,
    IProjectRepository projects,
    IProjectRoleRepository roles,
    IProjectMemberRepository members,
    IUnitOfWork unitOfWork,
    IOptions<SeedOptions> seedOptions,
    TimeProvider timeProvider,
    ILogger<UserProvisioningService> logger) : IUserProvisioningService
{
    public async Task<Result> UpsertAsync(InternalUserRequest request, CancellationToken cancellationToken = default)
    {
        var now = timeProvider.GetUtcNow().UtcDateTime;
        var profile = await profiles.GetByIdAsync(request.UserId, cancellationToken);
        if (profile is null)
        {
            await profiles.CreateAsync(new UserProfile
            {
                UserId = request.UserId,
                Username = request.Username.Trim(),
                Email = request.Email.Trim(),
                FirstName = UserService.NullIfBlank(request.FirstName),
                LastName = UserService.NullIfBlank(request.LastName),
                CreatedAt = now,
                UpdatedAt = now
            }, cancellationToken);
            logger.LogInformation("Profile created for user {UserId}", request.UserId);
        }
        else
        {
            profile.Username = request.Username.Trim();
            profile.Email = request.Email.Trim();
            profile.UpdatedAt = now;
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    public async Task<Result> ConfirmedAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var profile = await profiles.GetByIdAsync(userId, cancellationToken);
        if (profile is null)
        {
            return UserErrors.NotFound;
        }

        var existing = (await members.GetProjectIdsByUserAsync(userId, cancellationToken)).ToHashSet();
        var isSeedAdmin = IsSeedAdmin(profile.Email);
        var now = timeProvider.GetUtcNow().UtcDateTime;
        var granted = new List<string>();

        foreach (var project in await projects.GetActiveWithDefaultRoleAsync(cancellationToken))
        {
            if (existing.Contains(project.Id))
            {
                continue;
            }

            var roleId = project.DefaultRoleId!.Value;
            if (isSeedAdmin && project.Slug == ProjectService.CoreSlug
                && await roles.GetByNameAsync(project.Id, CoreRoles.Admin, cancellationToken) is { } adminRole)
            {
                roleId = adminRole.Id;
            }

            await members.CreateAsync(new ProjectMember
            {
                UserId = userId,
                ProjectId = project.Id,
                ProjectRoleId = roleId,
                GrantedAt = now,
                UpdatedAt = now
            }, cancellationToken);
            granted.Add(project.Slug);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        if (granted.Count > 0)
        {
            logger.LogInformation("Default roles granted to user {UserId} in {Projects}", userId, string.Join(", ", granted));
        }

        return Result.Success();
    }

    private bool IsSeedAdmin(string email)
    {
        var adminEmail = seedOptions.Value.AdminEmail;
        return !string.IsNullOrWhiteSpace(adminEmail) && string.Equals(adminEmail.Trim(), email, StringComparison.OrdinalIgnoreCase);
    }
}
