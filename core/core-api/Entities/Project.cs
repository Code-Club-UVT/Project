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

    /// <summary>
    /// Where the gateway forwards API calls (<c>/api/{Slug}</c>). May include a path, for example
    /// <c>http://core-api:8080/api</c>. Null means the project has no backend.
    /// </summary>
    public string? BackendUrl { get; set; }

    /// <summary>
    /// Internal address of the project's static frontend; the gateway serves it at <c>/apps/{Slug}/</c> and the harness
    /// shows it in a tab. Null means the project has no frontend. Never sent to browsers.
    /// </summary>
    public string? FrontendUrl { get; set; }

    /// <summary>Full URL the gateway probes to see whether the API is up. Optional, only meaningful with a backend.</summary>
    public string? HealthUrl { get; set; }

    public ProjectTokenMode TokenMode { get; set; } = ProjectTokenMode.Exchange;
    public bool IsActive { get; set; } = true;

    /// <summary>Created by the app (<c>core</c>, <c>identity</c>). Cannot be deleted or deactivated.</summary>
    public bool IsSystem { get; set; }

    /// <summary>
    /// The role every new user gets in this project once their email is confirmed. Null means invite-only: an admin
    /// grants access by hand. Not retroactive: setting it later does not change existing users.
    /// </summary>
    public Guid? DefaultRoleId { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public ICollection<ProjectRole> Roles { get; set; } = new List<ProjectRole>();
    public ICollection<ProjectMember> Members { get; set; } = new List<ProjectMember>();
}
