namespace CoreApi.Models.Responses;

public sealed record ProjectResponse(
    Guid Id,
    string Slug,
    string Name,
    string? Description,
    string BaseUrl,
    string? HealthUrl,
    string TokenMode,
    bool IsActive,
    bool IsSystem,
    DateTime CreatedAt,
    DateTime UpdatedAt);
