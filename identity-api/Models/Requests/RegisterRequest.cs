using System.ComponentModel.DataAnnotations;
using Identity.Common;

namespace Identity.Models.Requests;

public sealed record RegisterRequest
{
    [Required, StringLength(50, MinimumLength = 3), RegularExpression(@"^[A-Za-z0-9_.\-]+$", ErrorMessage = "Username may contain only letters, digits, '_', '.' and '-'.")]
    public string Username { get; init; } = string.Empty;

    [Required, EmailAddress, StringLength(256)]
    public string Email { get; init; } = string.Empty;

    [Required, StringLength(PasswordRules.MaxLength, MinimumLength = PasswordRules.MinLength)]
    public string Password { get; init; } = string.Empty;

    [StringLength(100)]
    public string? FirstName { get; init; }

    [StringLength(100)]
    public string? LastName { get; init; }
}
