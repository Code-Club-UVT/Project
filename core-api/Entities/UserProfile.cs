namespace CoreApi.Entities;

public class UserProfile
{
    public Guid UserId { get; set; }
    public string? FirstName { get; set; }
    public string? LastName { get; set; }

    public User User { get; set; } = null!;
}
