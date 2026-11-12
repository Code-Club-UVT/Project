using System.ComponentModel.DataAnnotations;
using Identity.Common;

namespace Identity.Models.Requests;

public sealed record LoginRequest
{
    /// <summary>Username or email.</summary>
    [Required, StringLength(256)]
    public string Identifier { get; init; } = string.Empty;

    [Required, StringLength(PasswordRules.MaxLength)]
    public string Password { get; init; } = string.Empty;
}
