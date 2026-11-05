using System.ComponentModel.DataAnnotations;

namespace Identity.Options;

public sealed class OutboxOptions
{
    public const string SectionName = "Outbox";

    /// <summary>How often the dispatcher looks for due messages.</summary>
    [Range(1, 300)]
    public int PollSeconds { get; set; } = 2;

    [Range(1, 500)]
    public int BatchSize { get; set; } = 20;

    /// <summary>After this many failed deliveries a message is marked failed and kept for inspection. With the backoff below, 30 attempts is about four hours.</summary>
    [Range(1, 1000)]
    public int MaxAttempts { get; set; } = 30;

    /// <summary>The wait after the first failure; it doubles on every further failure, up to <see cref="MaxBackoffSeconds"/>.</summary>
    [Range(1, 3600)]
    public int FirstBackoffSeconds { get; set; } = 2;

    [Range(1, 86400)]
    public int MaxBackoffSeconds { get; set; } = 600;

    /// <summary>How long a claimed message is reserved for the dispatcher that took it (must exceed one delivery's timeout).</summary>
    [Range(10, 3600)]
    public int LeaseSeconds { get; set; } = 60;
}

/// <summary>Where the services identity calls live, with the key each of them expects in <c>X-Internal-Key</c>.</summary>
public sealed class ServicesOptions
{
    public const string SectionName = "Services";

    [Required]
    public ServiceEndpointOptions Core { get; set; } = new();

    [Required]
    public ServiceEndpointOptions Email { get; set; } = new();

    [Range(1, 60)]
    public int TimeoutSeconds { get; set; } = 10;
}

public sealed class ServiceEndpointOptions
{
    /// <summary>Root URL of the service (no <c>/api</c>), for example <c>http://core-api:8080</c>.</summary>
    [Required, Url]
    public string Url { get; set; } = string.Empty;

    [Required, MinLength(32)]
    public string Key { get; set; } = string.Empty;
}
