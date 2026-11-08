namespace CoreApi.Options;

public sealed class SeedOptions
{
    public const string SectionName = "Seed";

    /// <summary>
    /// The email of the first admin (the same as identity's <c>Seed:Admin:Email</c>). When that user's email is confirmed,
    /// they get the <c>Admin</c> role on the core project instead of its default role. Optional.
    /// </summary>
    public string? AdminEmail { get; set; }

    /// <summary>Where the gateway reaches this API; the <c>core</c> project row is created or updated from it.</summary>
    public SeedProjectOptions CoreProject { get; set; } = new();

    /// <summary>Where the gateway reaches identity; the <c>identity</c> project row is created or updated from it.</summary>
    public SeedProjectOptions IdentityProject { get; set; } = new();
}

public sealed class SeedProjectOptions
{
    /// <summary>Backend URL including the <c>/api</c> path, for example <c>http://core-api:8080/api</c>.</summary>
    public string? BackendUrl { get; set; }

    public string? HealthUrl { get; set; }
}
