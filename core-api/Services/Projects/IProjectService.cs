using CoreApi.Common.Results;
using CoreApi.Models.Requests;
using CoreApi.Models.Responses;

namespace CoreApi.Services.Projects;

public interface IProjectService
{
    Task<Result<PagedResult<ProjectResponse>>> GetPagedAsync(int page, int pageSize, CancellationToken cancellationToken = default);
    Task<Result<ProjectResponse>> GetByIdAsync(Guid projectId, CancellationToken cancellationToken = default);
    Task<Result<ProjectResponse>> CreateAsync(CreateProjectRequest request, CancellationToken cancellationToken = default);
    Task<Result<ProjectResponse>> UpdateAsync(Guid projectId, UpdateProjectRequest request, CancellationToken cancellationToken = default);
    Task<Result> DeleteAsync(Guid projectId, CancellationToken cancellationToken = default);
}
