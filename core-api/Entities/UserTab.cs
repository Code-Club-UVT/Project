namespace CoreApi.Entities;

/// <summary>
/// An app the user has open in the harness, so the tabs come back after logging in again. The key is user + project
/// (one tab per app); the foreign key to the membership removes the tab when the user loses access or the project goes.
/// </summary>
public class UserTab
{
    public Guid UserId { get; set; }
    public Guid ProjectId { get; set; }

    /// <summary>Order of the tab, from 0.</summary>
    public int Position { get; set; }

    /// <summary>Where the user was inside the app (an app-internal path such as <c>/about</c>).</summary>
    public string LastPath { get; set; } = "/";

    public DateTime UpdatedAt { get; set; }

    public ProjectMember Member { get; set; } = null!;
}
