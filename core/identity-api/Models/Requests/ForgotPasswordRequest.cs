using System.ComponentModel.DataAnnotations;

namespace Identity.Models.Requests;

public sealed record ForgotPasswordRequest
{
    [Required, EmailAddress, StringLength(256)]
    public string Email { get; init; } = string.Empty;
}
