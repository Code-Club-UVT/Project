using System.ComponentModel.DataAnnotations;

namespace GatewayAuth.Options;

/// <summary>Settings an API needs to validate the project tokens the core API issues for it.</summary>
public sealed class GatewayAuthOptions
{
    public const string SectionName = "GatewayAuth";

    /// <summary>Must equal the <c>iss</c> of the tokens (core API: <c>Jwt:Issuer</c>).</summary>
    [Required]
    public string Issuer { get; set; } = string.Empty;

    /// <summary>This API's project slug. Tokens are only accepted when their <c>aud</c> is exactly this.</summary>
    [Required]
    public string Audience { get; set; } = string.Empty;

    /// <summary>Where the public signing keys are published, for example <c>http://core-api:8080/.well-known/jwks.json</c>.</summary>
    [Required]
    public string JwksUrl { get; set; } = string.Empty;

    /// <summary>Allows an http (not https) <see cref="JwksUrl"/>. For local development and a private Docker network only.</summary>
    public bool AllowInsecureJwksUrl { get; set; }

    /// <summary>How long the downloaded keys are used before they are fetched again.</summary>
    [Range(1, 1440)]
    public int KeysRefreshMinutes { get; set; } = 60;

    /// <summary>Minimum time between forced refreshes when a token names a key that is not known yet (key rotation).</summary>
    [Range(1, 60)]
    public int UnknownKeyRefreshMinutes { get; set; } = 1;
}
