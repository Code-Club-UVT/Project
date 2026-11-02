using System.ComponentModel.DataAnnotations;

namespace CoreApi.Models.Requests;

public sealed record ConfirmEmailRequest
{
    [Required, StringLength(256)]
    public string Token { get; init; } = string.Empty;
}
