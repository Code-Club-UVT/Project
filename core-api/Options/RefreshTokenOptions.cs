using System.ComponentModel.DataAnnotations;

namespace CoreApi.Options;

public sealed class RefreshTokenOptions
{
    public const string SectionName = "RefreshToken";

    [Range(1, 365)]
    public int Days { get; set; } = 7;

    /// <summary>Hard cap since the original login; rotation cannot extend a session beyond it.</summary>
    [Range(1, 365)]
    public int AbsoluteDays { get; set; } = 30;

    [Required]
    public string CookieName { get; set; } = "refreshToken";

    public bool CookieSecure { get; set; } = true;
}
