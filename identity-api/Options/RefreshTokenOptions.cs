using System.ComponentModel.DataAnnotations;

namespace Identity.Options;

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

    /// <summary>Public path the cookie is sent to: the gateway path of the identity auth endpoints (with the environment prefix when deployed).</summary>
    [Required]
    public string CookiePath { get; set; } = "/api/identity/auth";

    public bool CookieSecure { get; set; } = true;

    /// <summary>
    /// Two requests that refresh with the same cookie at the same moment (two tabs) are not a theft: a token rotated this
    /// many seconds ago still gets a new access token (never a new refresh token). 0 turns it off; after that time a
    /// reused token revokes the whole session family.
    /// </summary>
    [Range(0, 60)]
    public int ReuseGraceSeconds { get; set; } = 10;
}
