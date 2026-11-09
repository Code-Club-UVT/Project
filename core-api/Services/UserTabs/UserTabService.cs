using CoreApi.Entities;
using CoreApi.Mappings;
using CoreApi.Models.Requests;
using CoreApi.Models.Responses;
using CoreApi.Repositories.Abstractions;
using PlatformKit.Results;

namespace CoreApi.Services.UserTabs;

public class UserTabService(
    IUserTabRepository tabs,
    IProjectMemberRepository members,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider) : IUserTabService
{
    public async Task<Result<IReadOnlyList<UserTabResponse>>> GetAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var saved = await tabs.GetByUserAsync(userId, cancellationToken);
        IReadOnlyList<UserTabResponse> items = saved
            .Where(t => t.Member.Project is { IsActive: true, FrontendUrl: not null })
            .Select(t => t.ToResponse())
            .ToList();

        return Result.Success(items);
    }

    public async Task<Result<IReadOnlyList<UserTabResponse>>> ReplaceAsync(Guid userId, SaveUserTabsRequest request, CancellationToken cancellationToken = default)
    {
        if (request.Tabs.Count > UserTabRules.MaxTabs)
        {
            return UserTabErrors.TooMany;
        }

        if (request.Tabs.Any(t => !UserTabRules.IsValidPath(t.LastPath)))
        {
            return UserTabErrors.InvalidPath;
        }

        // Only apps the user can open are kept; the foreign key to the membership enforces it in the database as well.
        var memberOf = (await members.GetProjectIdsByUserAsync(userId, cancellationToken)).ToHashSet();
        var wanted = request.Tabs
            .Where(t => memberOf.Contains(t.ProjectId))
            .DistinctBy(t => t.ProjectId)
            .ToList();

        var now = timeProvider.GetUtcNow().UtcDateTime;
        var existing = (await tabs.GetTrackedByUserAsync(userId, cancellationToken)).ToDictionary(t => t.ProjectId);

        // Update in place, add the new ones and remove the rest: a row is never deleted and re-added with the same key.
        foreach (var gone in existing.Values.Where(t => wanted.All(w => w.ProjectId != t.ProjectId)))
        {
            await tabs.DeleteAsync(gone, cancellationToken);
        }

        for (var position = 0; position < wanted.Count; position++)
        {
            var item = wanted[position];
            if (existing.TryGetValue(item.ProjectId, out var tab))
            {
                tab.Position = position;
                tab.LastPath = item.LastPath;
                tab.UpdatedAt = now;
            }
            else
            {
                await tabs.CreateAsync(new UserTab
                {
                    UserId = userId,
                    ProjectId = item.ProjectId,
                    Position = position,
                    LastPath = item.LastPath,
                    UpdatedAt = now
                }, cancellationToken);
            }
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);

        return await GetAsync(userId, cancellationToken);
    }
}
