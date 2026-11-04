using CoreApi.Entities;

namespace CoreApi.Services.Tokens;

public interface ITokenService
{
    /// <summary>The edge token the SPA holds: audience = the gateway, carries the user's global role.</summary>
    AccessToken CreateAccessToken(User user);

    /// <summary>A short-lived token for one project: audience = the project slug, <c>role</c> = the user's role in that project.</summary>
    AccessToken CreateProjectToken(User user, Project project, ProjectRole role);

    /// <summary>Creates an opaque, cryptographically random token (for refresh tokens and emailed links).</summary>
    string GenerateToken();

    /// <summary>SHA-256 (hex) of a token; this is what gets stored.</summary>
    string Hash(string token);
}
