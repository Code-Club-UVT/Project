namespace CoreApi.Models.Responses;

/// <summary>
/// A project the current user can open, with their role in it. The URLs stay internal: a frontend is opened at
/// <c>/apps/{Slug}/</c> and the API is called at <c>/api/{Slug}</c>.
/// </summary>
public sealed record MyProjectResponse(
    Guid ProjectId,
    string Slug,
    string Name,
    string? Description,
    Guid RoleId,
    string RoleName,
    bool HasFrontend,
    bool HasBackend);
