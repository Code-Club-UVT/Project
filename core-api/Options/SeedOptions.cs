namespace CoreApi.Options;

/// <summary>Optional first-admin seed. Nothing is created unless all three values are set.</summary>
public sealed class SeedOptions
{
    public const string SectionName = "Seed";

    public SeedAdminOptions Admin { get; set; } = new();
    public SeedCoreProjectOptions CoreProject { get; set; } = new();
}

public sealed class SeedAdminOptions
{
    public string? Username { get; set; }
    public string? Email { get; set; }
    public string? Password { get; set; }
}

/// <summary>Where the gateway reaches this API. The "core" project row is created or updated from these values at startup.</summary>
public sealed class SeedCoreProjectOptions
{
    /// <summary>Base URL including the <c>/api</c> path, for example <c>http://core-api:8080/api</c>.</summary>
    public string? BaseUrl { get; set; }

    public string? HealthUrl { get; set; }
}
