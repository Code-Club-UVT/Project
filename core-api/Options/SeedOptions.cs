namespace CoreApi.Options;

/// <summary>Optional first-admin seed. Nothing is created unless all three values are set.</summary>
public sealed class SeedOptions
{
    public const string SectionName = "Seed";

    public SeedAdminOptions Admin { get; set; } = new();
}

public sealed class SeedAdminOptions
{
    public string? Username { get; set; }
    public string? Email { get; set; }
    public string? Password { get; set; }
}
