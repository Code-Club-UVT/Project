using System.ComponentModel.DataAnnotations;

namespace CoreApi.Options;

/// <summary>Settings for the endpoints that only the gateway may call.</summary>
public sealed class InternalOptions
{
    public const string SectionName = "Internal";
    public const string PlaceholderKeyPrefix = "CHANGE-ME";

    /// <summary>Shared secret the gateway sends in the <c>X-Internal-Key</c> header.</summary>
    [Required, MinLength(32)]
    public string ApiKey { get; set; } = string.Empty;
}
