namespace CoreApi.Entities;

/// <summary>A custom role defined for one project. Its name becomes the <c>role</c> claim of that project's tokens.</summary>
public class ProjectRole
{
    public Guid Id { get; set; }
    public Guid ProjectId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public Project Project { get; set; } = null!;
    public ICollection<ProjectMember> Members { get; set; } = new List<ProjectMember>();
}
