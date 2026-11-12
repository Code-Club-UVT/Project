using System.ComponentModel.DataAnnotations;

namespace Identity.Options;

public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    [Required]
    public string Issuer { get; set; } = string.Empty;

    /// <summary>Audience of the access (edge) token: the gateway. Project tokens use the project slug instead.</summary>
    [Required]
    public string Audience { get; set; } = string.Empty;

    /// <summary>
    /// ECDSA P-256 private key in PEM (PKCS#8 or SEC1). Every token is signed with it; the public part is published as a JWKS.
    /// Empty is allowed in Development only (a temporary key is generated at startup). Literal "\n" sequences are turned into
    /// new lines so the PEM fits in a single environment variable.
    /// </summary>
    public string? PrivateKeyPem { get; set; }

    [Range(1, 1440)]
    public int AccessTokenMinutes { get; set; } = 15;

    [Range(1, 60)]
    public int ProjectTokenMinutes { get; set; } = 5;
}
