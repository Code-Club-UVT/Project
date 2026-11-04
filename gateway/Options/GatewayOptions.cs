using System.ComponentModel.DataAnnotations;

namespace Gateway.Options;

/// <summary>How the gateway reaches the core API's internal endpoints (token exchange).</summary>
public sealed class CoreApiOptions
{
    public const string SectionName = "CoreApi";
    public const string PlaceholderKeyPrefix = "CHANGE-ME";

    /// <summary>Root URL of the core API, without <c>/api</c>, for example <c>http://core-api:8080</c>.</summary>
    [Required, Url]
    public string InternalUrl { get; set; } = string.Empty;

    /// <summary>Shared secret sent as <c>X-Internal-Key</c>; must equal the core API's <c>Internal:ApiKey</c>.</summary>
    [Required, MinLength(32)]
    public string InternalKey { get; set; } = string.Empty;

    [Range(1, 60)]
    public int TimeoutSeconds { get; set; } = 5;
}

/// <summary>How fresh the data from the database has to be, and how long old data may be used if the database is down.</summary>
public sealed class RoutingOptions
{
    public const string SectionName = "Routing";

    /// <summary>How often the project list is read to update the routes.</summary>
    [Range(1, 600)]
    public int PollSeconds { get; set; } = 30;

    /// <summary>How long a user's access to a project is cached. A revoked grant or a disabled user can keep working this long.</summary>
    [Range(1, 600)]
    public int AccessCacheSeconds { get; set; } = 30;

    /// <summary>When the database cannot be read, an entry older than its cache time but younger than this is still used. After that: 503.</summary>
    [Range(1, 3600)]
    public int StaleGraceSeconds { get; set; } = 300;
}

public sealed class RateLimitOptions
{
    public const string SectionName = "RateLimiting";

    /// <summary>Per client IP, for the core API's auth endpoints (login, register, ...).</summary>
    [Range(1, 100000)]
    public int AuthPerMinute { get; set; } = 10;

    /// <summary>Per user (or per IP when anonymous), for everything else.</summary>
    [Range(1, 1000000)]
    public int DefaultPerMinute { get; set; } = 300;
}

public sealed class FrontendCorsOptions
{
    public const string SectionName = "Cors";

    /// <summary>Origins of the single-page apps that may call the gateway with credentials.</summary>
    [Required, MinLength(1)]
    public string[] AllowedOrigins { get; set; } = [];
}
