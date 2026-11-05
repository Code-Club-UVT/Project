using System.ComponentModel.DataAnnotations;

namespace EmailApi.Models;

/// <param name="MessageId">Chosen by the caller (its outbox row id). The same id sent again is not sent twice.</param>
/// <param name="Template">A template name, for example <c>email-confirmation</c>.</param>
/// <param name="To">The recipient address.</param>
/// <param name="Data">The values the template needs (for example <c>link</c>). Plain text; the template encodes them.</param>
public sealed record SendEmailRequest(
    [property: Required] Guid? MessageId,
    [property: Required, MaxLength(100)] string Template,
    [property: Required, EmailAddress, MaxLength(320)] string To,
    Dictionary<string, string>? Data);
