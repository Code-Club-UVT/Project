using CoreApi.Enums;

namespace CoreApi.Entities;

/// <summary>An API behind the gateway. It is reachable at <c>/api/{Slug}</c> on the gateway.</summary>
public class Project
{
    public Guid Id { get; set; }

    /// <summary>Lowercase letters, digits and hyphens. Part of the public path, so it never changes.</summary>
    public string Slug { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }

    /// <summary>Where the gateway forwards to. May include a path, for example <c>http://core-api:8080/api</c>.</summary>
    public string BaseUrl { get; set; } = string.Empty;

    /// <summary>Full URL the gateway probes to see whether the API is up. Optional.</summary>
    public string? HealthUrl { get; set; }

    public ProjectTokenMode TokenMode { get; set; } = ProjectTokenMode.Exchange;
    public bool IsActive { get; set; } = true;

    /// <summary>Created by the app (the core API). Cannot be deleted or deactivated.</summary>
    public bool IsSystem { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public ICollection<ProjectRole> Roles { get; set; } = new List<ProjectRole>();
    public ICollection<ProjectMember> Members { get; set; } = new List<ProjectMember>();
}
