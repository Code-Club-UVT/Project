namespace Identity.Options;

/// <summary>Optional first-admin seed. Nothing is created unless all three values are set.</summary>
public sealed class SeedOptions
{
    public const string SectionName = "Seed";

    public SeedAdminOptions Admin { get; set; } = new();
}

/// <summary>
/// The account is created already confirmed. It becomes an admin because the core API's <c>Seed:AdminEmail</c> holds
/// the same email: when the core API hears about the confirmation, it grants that user the <c>Admin</c> role on <c>core</c>.
/// </summary>
public sealed class SeedAdminOptions
{
    public string? Username { get; set; }
    public string? Email { get; set; }
    public string? Password { get; set; }
}
