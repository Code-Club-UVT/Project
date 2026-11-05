using System.ComponentModel.DataAnnotations;
using Identity.Common;

namespace Identity.Models.Requests;

public sealed record ResetPasswordRequest
{
    [Required, StringLength(256)]
    public string Token { get; init; } = string.Empty;

    [Required, StringLength(PasswordRules.MaxLength, MinimumLength = PasswordRules.MinLength)]
    public string NewPassword { get; init; } = string.Empty;
}
