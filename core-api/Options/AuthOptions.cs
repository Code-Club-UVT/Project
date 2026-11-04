using System.ComponentModel.DataAnnotations;

namespace CoreApi.Options;

public sealed class AuthOptions
{
    public const string SectionName = "Auth";

    [Range(1, 720)]
    public int EmailConfirmationHours { get; set; } = 24;

    [Range(1, 72)]
    public int PasswordResetHours { get; set; } = 1;
}
