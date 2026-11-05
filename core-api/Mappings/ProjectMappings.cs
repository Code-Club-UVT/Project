using CoreApi.Entities;
using CoreApi.Models.Responses;

namespace CoreApi.Mappings;

public static class ProjectMappings
{
    public static ProjectResponse ToResponse(this Project project)
    {
        return new ProjectResponse(
            project.Id,
            project.Slug,
            project.Name,
            project.Description,
            project.BaseUrl,
            project.HealthUrl,
            project.TokenMode.ToString(),
            project.IsActive,
            project.IsSystem,
            project.DefaultRoleId,
            project.CreatedAt,
            project.UpdatedAt);
    }

    public static ProjectRoleResponse ToResponse(this ProjectRole role)
    {
        return new ProjectRoleResponse(role.Id, role.ProjectId, role.Name, role.Description, role.CreatedAt, role.UpdatedAt);
    }

    /// <summary>Needs <see cref="ProjectMember.User"/> and <see cref="ProjectMember.Role"/> loaded.</summary>
    public static ProjectMemberResponse ToResponse(this ProjectMember member)
    {
        return new ProjectMemberResponse(
            member.UserId,
            member.User.Username,
            member.User.Email,
            member.ProjectRoleId,
            member.Role.Name,
            member.GrantedByUserId,
            member.GrantedAt);
    }

    /// <summary>Needs <see cref="ProjectMember.Project"/> and <see cref="ProjectMember.Role"/> loaded.</summary>
    public static MyProjectResponse ToMyProjectResponse(this ProjectMember member)
    {
        return new MyProjectResponse(
            member.ProjectId,
            member.Project.Slug,
            member.Project.Name,
            member.Project.Description,
            member.ProjectRoleId,
            member.Role.Name);
    }
}
