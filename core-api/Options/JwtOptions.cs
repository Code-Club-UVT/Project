using System.ComponentModel.DataAnnotations;

namespace CoreApi.Options;

public sealed class JwtOptions
{
    public const string SectionName = "Jwt";
    public const string PlaceholderKeyPrefix = "CHANGE-ME";

    [Required]
    public string Issuer { get; set; } = string.Empty;

    [Required]
    public string Audience { get; set; } = string.Empty;

    [Required, MinLength(32)]
    public string SigningKey { get; set; } = string.Empty;

    [Range(1, 1440)]
    public int AccessTokenMinutes { get; set; } = 15;
}
