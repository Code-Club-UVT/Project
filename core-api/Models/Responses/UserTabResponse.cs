namespace CoreApi.Models.Responses;

/// <summary>An open tab: the project (its app) and the path the user was at inside it.</summary>
public sealed record UserTabResponse(Guid ProjectId, string LastPath);
