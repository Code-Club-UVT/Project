namespace CoreApi.Models.Responses;

public sealed record ProjectMemberResponse(
    Guid UserId,
    string Username,
    string Email,
    Guid RoleId,
    string RoleName,
    Guid? GrantedByUserId,
    DateTime GrantedAt);
