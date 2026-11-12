namespace CoreApi.Models.Responses;

public sealed record ProjectRoleResponse(
    Guid Id,
    Guid ProjectId,
    string Name,
    string? Description,
    DateTime CreatedAt,
    DateTime UpdatedAt);
