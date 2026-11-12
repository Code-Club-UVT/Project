using System.ComponentModel.DataAnnotations;

namespace Identity.Models.Requests;

public sealed record ConfirmEmailRequest
{
    [Required, StringLength(256)]
    public string Token { get; init; } = string.Empty;
}
