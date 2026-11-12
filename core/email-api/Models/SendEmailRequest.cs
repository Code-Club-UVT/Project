using System.ComponentModel.DataAnnotations;

namespace EmailApi.Models;

public sealed record SendEmailRequest
{
    /// <summary>Chosen by the caller (its outbox row id). The same id sent again is not sent twice.</summary>
    [Required]
    public Guid? MessageId { get; init; }

    /// <summary>A template name, for example <c>email-confirmation</c>.</summary>
    [Required, MaxLength(100)]
    public string Template { get; init; } = string.Empty;

    /// <summary>The recipient address.</summary>
    [Required, EmailAddress, MaxLength(320)]
    public string To { get; init; } = string.Empty;

    /// <summary>The values the template needs (for example <c>link</c>). Plain text; the template encodes them.</summary>
    public Dictionary<string, string>? Data { get; init; }
}
