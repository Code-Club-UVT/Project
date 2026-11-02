using System.ComponentModel.DataAnnotations;

namespace CoreApi.Models.Requests;

public sealed record ResendConfirmationRequest
{
    [Required, EmailAddress, StringLength(256)]
    public string Email { get; init; } = string.Empty;
}
