namespace PlatformKit.RequestContext;

/// <summary>Who is calling and which request this is, for the current HTTP request. Register it with <c>AddRequestContext()</c>.</summary>
public interface IRequestContext
{
    bool IsAuthenticated { get; }

    /// <summary>The user id (<c>sub</c>); null when the request is anonymous.</summary>
    Guid? UserId { get; }

    string? Username { get; }

    /// <summary>The user's role in this project (the <c>role</c> claim); null when anonymous.</summary>
    string? Role { get; }

    /// <summary>This API's slug, from <c>PlatformAuth:Audience</c>.</summary>
    string ProjectSlug { get; }

    /// <summary>Taken from <c>X-Correlation-Id</c> when valid, otherwise generated. Same value in logs, the response and outgoing calls.</summary>
    string CorrelationId { get; }

    /// <summary>The caller's IP as seen by this API. Behind the gateway that is the gateway unless forwarded headers are configured.</summary>
    string? ClientIp { get; }
}
