namespace CoreApi.Entities;

/// <summary>A user's access to a project, with exactly one role (the key is user + project).</summary>
public class ProjectMember
{
    public Guid UserId { get; set; }
    public Guid ProjectId { get; set; }
    public Guid ProjectRoleId { get; set; }

    /// <summary>The admin who granted the access; null if that account was deleted.</summary>
    public Guid? GrantedByUserId { get; set; }

    public DateTime GrantedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public User User { get; set; } = null!;
    public Project Project { get; set; } = null!;
    public ProjectRole Role { get; set; } = null!;
}
