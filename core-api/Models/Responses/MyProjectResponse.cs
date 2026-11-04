namespace CoreApi.Models.Responses;

/// <summary>A project the current user can open, with their role in it.</summary>
public sealed record MyProjectResponse(
    Guid ProjectId,
    string Slug,
    string Name,
    string? Description,
    Guid RoleId,
    string RoleName);
