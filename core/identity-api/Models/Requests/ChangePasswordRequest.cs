using System.ComponentModel.DataAnnotations;
using Identity.Common;

namespace Identity.Models.Requests;

public sealed record ChangePasswordRequest
{
    [Required, StringLength(PasswordRules.MaxLength)]
    public string CurrentPassword { get; init; } = string.Empty;

    [Required, StringLength(PasswordRules.MaxLength, MinimumLength = PasswordRules.MinLength)]
    public string NewPassword { get; init; } = string.Empty;
}
