using System.ComponentModel.DataAnnotations;

namespace CoreApi.Options;

public sealed class FrontendOptions
{
    public const string SectionName = "Frontend";

    [Required, Url]
    public string BaseUrl { get; set; } = string.Empty;

    [Required]
    public string ConfirmEmailPath { get; set; } = "/confirm-email";

    [Required]
    public string ResetPasswordPath { get; set; } = "/reset-password";
}
