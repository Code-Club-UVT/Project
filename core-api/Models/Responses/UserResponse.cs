namespace CoreApi.Models.Responses;

public sealed record UserResponse(
    Guid Id,
    string Username,
    string Email,
    string Role,
    bool EmailConfirmed,
    string? FirstName,
    string? LastName,
    DateTime CreatedAt);
