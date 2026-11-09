using System.ComponentModel.DataAnnotations;

namespace CoreApi.Models.Requests;

/// <summary>The user's whole list of open tabs, in order. It replaces the saved list.</summary>
public sealed record SaveUserTabsRequest
{
    [Required]
    public List<UserTabRequest> Tabs { get; init; } = [];
}

public sealed record UserTabRequest
{
    public Guid ProjectId { get; init; }

    /// <summary>An app-internal path starting with a single "/", such as <c>/about</c>.</summary>
    [Required]
    [StringLength(500)]
    public string LastPath { get; init; } = "/";
}
